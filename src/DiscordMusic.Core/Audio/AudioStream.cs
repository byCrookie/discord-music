using System.IO.Abstractions;
using System.IO.Pipelines;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;
using ValueOf;

namespace DiscordMusic.Core.Audio;

internal static partial class AudioStreamLogMessages
{
    [LoggerMessage(Message = "[{Id}] Stream ended", Level = LogLevel.Trace)]
    internal static partial void LogStreamEnded(this ILogger logger, string id);

    [LoggerMessage(Message = "[{Id}] Stream failed", Level = LogLevel.Error)]
    internal static partial void LogStreamFailed(this ILogger logger, string id);

    [LoggerMessage(Message = "[{Id}] Audio streaming was cancelled", Level = LogLevel.Trace)]
    internal static partial void LogStreamCancelled(this ILogger logger, string id);
}

public class AudioStream : IDisposable
{
    public enum AudioState
    {
        Playing,
        Silence,
        Ended,
        Stopped,
    }

    public enum SeekMode
    {
        Position,
        Forward,
        Backward,
    }

    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;

    // Send data to NetCord in small chunks.
    // This allows to check for Cancellation (Seek/Stop) many times per second.
    // If increased, the bot would feel "laggy" when skipping songs.
    // 3840 bytes = 20ms of audio
    private const int NetworkFrameSize = 3840 * 5;

    // Read from disk in large 300ms chunks.
    // This is efficient for the OS and reduces CPU overhead.
    // 57600 bytes = 300ms of audio
    private const int DiskReadSize = 57600;

    // Pipe buffer size is double the disk read size to allow
    // some leeway between reading from disk and sending to network.
    private const int PipeBufferSize = DiskReadSize * 20;

    private readonly string _id;
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Lock _lock;

    private Pipe _pipe = null!;
    private CancellationTokenSource _producerCts = null!;
    private CancellationTokenSource _consumerCts;

    private AudioStream(
        Stream inputStream,
        Stream outputStream,
        ILogger logger,
        CancellationToken ct
    )
    {
        _id = Guid.NewGuid().ToString("N");
        _inputStream = inputStream;
        _outputStream = outputStream;
        _logger = logger;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _lock = new Lock();
        _consumerCts = new CancellationTokenSource();

        SetupPipeAndTasks();

        _ = Task.Factory.StartNew(
            StateMachineLoop,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    private void SetupPipeAndTasks()
    {
        _pipe = new Pipe(
            new PipeOptions(
                pauseWriterThreshold: PipeBufferSize,
                resumeWriterThreshold: PipeBufferSize / 2,
                useSynchronizationContext: false
            )
        );

        _producerCts = new CancellationTokenSource();
        _ = FillPipeAsync(_producerCts.Token);

        _consumerCts = new CancellationTokenSource();
    }

    private async Task FillPipeAsync(CancellationToken ct)
    {
        var writer = _pipe.Writer;

        while (!ct.IsCancellationRequested)
        {
            // Allocate a large buffer for efficient disk IO
            var memory = writer.GetMemory(DiskReadSize);
            try
            {
                // Read 0.3s worth of audio at once
                var bytesRead = await _inputStream.ReadAsync(memory, ct);
                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                await writer.CompleteAsync(ex);
                return;
            }

            // FlushAsync will pause here if the pipe is full.
            // This effectively throttles the disk reader to match the playback speed.
            var result = await writer.FlushAsync(ct);
            if (result.IsCompleted)
            {
                break;
            }
        }

        await writer.CompleteAsync();
    }

    private async Task HandlePlayingAsync()
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token,
            // ReSharper disable once InconsistentlySynchronizedField
            _consumerCts.Token
        );

        var token = linkedCts.Token;

        var sourceStream = _pipe.Reader.AsStream(true);

        try
        {
            await sourceStream.CopyToAsync(_outputStream, NetworkFrameSize, token);

            State = AudioState.Ended;
            _logger.LogStreamEnded(_id);
            if (StreamEnded is not null)
            {
                await StreamEnded(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during Seek or Stop
        }
    }

    private async Task StateMachineLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                switch (State)
                {
                    case AudioState.Playing:
                        await HandlePlayingAsync();
                        break;
                    case AudioState.Silence:
                    case AudioState.Ended:
                    case AudioState.Stopped:
                        await Task.Delay(100);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(State), State, null);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogStreamCancelled(_id);
        }
        catch (Exception e)
        {
            _logger.LogStreamFailed(_id);
            State = AudioState.Stopped;
            if (StreamFailed is not null)
            {
                await StreamFailed(e, this, EventArgs.Empty);
            }
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (State == AudioState.Silence)
            {
                return;
            }

            State = AudioState.Silence;

            // Cancel the current token to force HandlePlayingAsync
            // to stop executing 'CopyToAsync' immediately.
            _consumerCts.Cancel();
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (State == AudioState.Playing)
            {
                return;
            }

            // Since Pause() cancelled the token, create a new one
            // before trying to play again, otherwise HandlePlayingAsync will
            // cancel immediately.
            if (_consumerCts.IsCancellationRequested)
            {
                _consumerCts.Dispose();
                _consumerCts = new CancellationTokenSource();
            }

            State = AudioState.Playing;
        }
    }

    public ErrorOr<TimeSpan> Seek(TimeSpan time, SeekMode seekMode)
    {
        lock (_lock)
        {
            var seekBytes = Bytes.ToBytes(time);
            var seekPosition = seekMode switch
            {
                SeekMode.Position => seekBytes,
                SeekMode.Forward => Bytes.From(_inputStream.Position) + seekBytes,
                SeekMode.Backward => Bytes.From(_inputStream.Position) - seekBytes,
                _ => throw new ArgumentOutOfRangeException(nameof(seekMode), seekMode, null),
            };

            if (seekPosition > _inputStream.Length)
            {
                seekPosition = Bytes.From(_inputStream.Length);
            }

            if (seekPosition < 0)
            {
                seekPosition = Bytes.From(0);
            }

            _consumerCts.Cancel();
            _consumerCts.Dispose();

            _producerCts.Cancel();
            _producerCts.Dispose();

            _inputStream.Position = seekPosition;
            State = AudioState.Playing;

            SetupPipeAndTasks();

            return seekPosition.ToTimeSpan();
        }
    }

    public AudioState State { get; private set; } = AudioState.Playing;
    public TimeSpan Length => Bytes.From(_inputStream.Length).ToTimeSpan();
    public TimeSpan Position => Bytes.From(_inputStream.Position).ToTimeSpan();

    public void Dispose()
    {
        State = AudioState.Stopped;
        _cts.Cancel();
        _producerCts.Cancel();
        _consumerCts.Cancel();
        StreamEnded = null;
        StreamFailed = null;
        _inputStream.Dispose();
        _cts.Dispose();
    }

    public event Func<Exception, object, EventArgs, Task>? StreamFailed;
    public event Func<object, EventArgs, Task>? StreamEnded;

    public static ErrorOr<AudioStream> Load(
        IFileInfo audioFile,
        Stream outputStream,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken ct
    )
    {
        if (!fileSystem.File.Exists(audioFile.FullName))
        {
            return Error.NotFound(description: $"Audio file '{audioFile.FullName}' does not exist");
        }

        var stream = new FileStream(audioFile.FullName, FileMode.Open, FileAccess.Read);
        logger.LogDebug("Audio stream loaded from {AudioFile}", audioFile.FullName);
        return new AudioStream(stream, outputStream, logger, ct);
    }

    public static ByteSize ApproxSize(TimeSpan time)
    {
        return ByteSize.FromBytes(Bytes.ToBytes(time));
    }

    private class Bytes : ValueOf<long, Bytes>
    {
        public TimeSpan ToTimeSpan() =>
            TimeSpan.FromSeconds(1d * Value / (SampleRate * Channels * BytesPerSample));

        public static Bytes ToBytes(TimeSpan time) =>
            From(
                (long)Math.Ceiling(1d * time.TotalSeconds * SampleRate * Channels * BytesPerSample)
            );

        public static Bytes operator +(Bytes a, Bytes b) => From(a.Value + b.Value);

        public static Bytes operator -(Bytes a, Bytes b) => From(a.Value - b.Value);

        public static bool operator >(Bytes a, long b) => a.Value > b;

        public static bool operator <(Bytes a, long b) => a.Value < b;

        public static implicit operator long(Bytes bytes) => bytes.Value;
    }
}
