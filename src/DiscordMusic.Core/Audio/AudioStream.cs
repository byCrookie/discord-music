using System.IO.Abstractions;
using System.IO.Pipelines;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;

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

    // PCM frame matching the 60ms Opus frame duration configured in NetCord.
    // 48kHz × 2 channels × 2 bytes × 0.06s = 11520 bytes
    private const int FrameSize = 11520;

    // Read from disk in large 300ms chunks.
    // This is efficient for the OS and reduces CPU overhead.
    // 57600 bytes = 300ms of audio
    private const int DiskReadSize = 57600;

    // Large read-ahead buffer (~10s) so playback is resilient
    // against slow or unreliable filesystem access.
    // Rate-limiting on the consumer side prevents flooding Discord.
    private const int PipeBufferSize = 192000 * 10;

    private readonly string _id;
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Lock _lock;

    private Pipe _pipe = null!;
    private CancellationTokenSource _producerCts = null!;
    private CancellationTokenSource _consumerCts;
    private bool _disposed;

    private long _playbackGeneration;

    public AudioState State { get; private set; }

    private AudioStream(
        AudioState initialState,
        Stream inputStream,
        Stream outputStream,
        ILogger logger,
        CancellationToken ct
    )
    {
        State = initialState;
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

        // New pipe => new generation.
        Interlocked.Increment(ref _playbackGeneration);
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
                if (bytesRead == 0 || ct.IsCancellationRequested)
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
        var generation = Volatile.Read(ref _playbackGeneration);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token,
            _consumerCts.Token
        );

        var token = linkedCts.Token;

        try
        {
            // If the pipe has already been completed due to a Seek/Dispose, trying to read will throw.
            // Fast-fail instead and let the state machine loop decide what to do next.
            if (token.IsCancellationRequested)
            {
                return;
            }

            // Read from the pre-loaded pipe one frame at a time.
            // The pipe holds ~10s of read-ahead from disk for filesystem
            // resilience. Writing one frame per iteration lets us check
            // cancellation every ~60ms. VoiceStream's SpeedNormalizingStream
            // back-pressures each WriteAsync to real-time pace, so no
            // explicit delay is needed here.
            var sourceStream = _pipe.Reader.AsStream(true);
            var sendBuffer = new byte[FrameSize];

            while (!token.IsCancellationRequested)
            {
                // Fill exactly one frame (may need multiple reads for a full frame)
                var totalRead = 0;
                while (totalRead < FrameSize)
                {
                    var bytesRead = await sourceStream.ReadAsync(
                        sendBuffer.AsMemory(totalRead, FrameSize - totalRead),
                        token
                    );
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }

                if (totalRead == 0)
                {
                    break;
                }

                await _outputStream.WriteAsync(
                    sendBuffer.AsMemory(0, totalRead),
                    token
                );
            }

            // If a Seek happened while we were copying, we must not transition to Ended.
            if (generation != Volatile.Read(ref _playbackGeneration))
            {
                return;
            }

            State = AudioState.Ended;
            _logger.LogStreamEnded(_id);
            if (StreamEnded is not null)
            {
                await StreamEnded(this, EventArgs.Empty);
            }
        }
        catch (InvalidOperationException)
        {
            // Can happen if a Seek/Dispose completed the reader while we're (re)entering playback.
            // Treat as a normal control-flow interruption.
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
            if (_disposed || State == AudioState.Silence)
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
            if (_disposed || State == AudioState.Playing)
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
            if (_disposed)
            {
                return Error.Unexpected(description: "Audio stream is disposed");
            }

            var seekBytes = Pcm16Bytes.ToBytes(time);
            var seekPosition = seekMode switch
            {
                SeekMode.Position => seekBytes,
                SeekMode.Forward => Pcm16Bytes.From(_inputStream.Position) + seekBytes,
                SeekMode.Backward => Pcm16Bytes.From(_inputStream.Position) - seekBytes,
                _ => throw new ArgumentOutOfRangeException(nameof(seekMode), seekMode, null),
            };

            if (seekPosition > _inputStream.Length)
            {
                seekPosition = Pcm16Bytes.From(_inputStream.Length);
            }

            if (seekPosition < 0)
            {
                seekPosition = Pcm16Bytes.From(0);
            }

            _consumerCts.Cancel();
            _producerCts.Cancel();

            try
            {
                _pipe.Writer.Complete();
                _pipe.Reader.Complete();
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Failed to complete pipe in Seek");
            }

            _inputStream.Position = seekPosition;

            _consumerCts.Dispose();
            _producerCts.Dispose();

            SetupPipeAndTasks();

            return seekPosition.ToTime();
        }
    }

    public TimeSpan Length
    {
        get
        {
            lock (_lock)
            {
                return Pcm16Bytes.From(_inputStream.Length).ToTime();
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_lock)
            {
                return Pcm16Bytes.From(_inputStream.Position).ToTime();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            State = AudioState.Stopped;
            StreamEnded = null;
            StreamFailed = null;

            Interlocked.Increment(ref _playbackGeneration);

            _cts.Cancel();
            _producerCts.Cancel();
            _consumerCts.Cancel();

            try
            {
                _pipe.Writer.Complete();
                _pipe.Reader.Complete();
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Failed to complete pipe in Dispose");
            }

            _inputStream.Dispose();
            _consumerCts.Dispose();
            _producerCts.Dispose();
            _cts.Dispose();
        }
    }

    public event Func<Exception, object, EventArgs, Task>? StreamFailed;
    public event Func<object, EventArgs, Task>? StreamEnded;

    public static ErrorOr<AudioStream> Load(
        AudioState initialState,
        IFileInfo audioFile,
        Stream outputStream,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken ct
    )
    {
        if (!fileSystem.File.Exists(audioFile.FullName))
        {
            return Error.NotFound(description: "Audio file not found.");
        }

        try
        {
            var stream = fileSystem.FileStream.New(
                audioFile.FullName,
                FileMode.Open,
                FileAccess.Read
            );
            logger.LogDebug("Audio stream loaded from {AudioFile}", audioFile.FullName);
            return new AudioStream(initialState, stream, outputStream, logger, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load audio stream from {AudioFile}", audioFile.FullName);
            return Error
                .Unexpected(
                    description: "Failed to load audio file.",
                    code: "AudioStream.LoadFailed"
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "audioStream.load")
                .WithMetadata("audioFile", audioFile.FullName)
                .WithException(e);
        }
    }
}
