using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ValueOf;

namespace DiscordMusic.Core.Audio;

internal static partial class AudioStreamLogMessages
{
    [LoggerMessage(
        Message = "[{Id}] Streaming {Type} ({Bytes})",
        Level = LogLevel.Trace)]
    internal static partial void LogStreaming(
        this ILogger logger,
        string id,
        string type,
        string bytes);

    [LoggerMessage(
        Message = "[{Id}] Stream ended",
        Level = LogLevel.Trace)]
    internal static partial void LogStreamEnded(
        this ILogger logger,
        string id);
    
    [LoggerMessage(
        Message = "[{Id}] Stream failed",
        Level = LogLevel.Error)]
    internal static partial void LogStreamFailed(
        this ILogger logger,
        string id);

    [LoggerMessage(
        Message = "[{Id}] Filled remaining buffer with silence",
        Level = LogLevel.Trace)]
    internal static partial void LogFilled(
        this ILogger logger,
        string id);

    [LoggerMessage(
        Message = "Audio {Position} / {Length}",
        Level = LogLevel.Trace)]
    internal static partial void LogPosition(
        this ILogger logger,
        string position,
        string length);

    [LoggerMessage(
        Message = "[{Id}] Still enough data in output stream",
        Level = LogLevel.Trace)]
    internal static partial void LogStillEnough(
        this ILogger logger,
        string id);
    
    [LoggerMessage(
        Message = "[{Id}] Audio streaming was cancelled",
        Level = LogLevel.Trace)]
    internal static partial void LogStreamCancelled(
        this ILogger logger,
        string id);
}

public class AudioStream : IDisposable
{
    public enum AudioState
    {
        Playing,
        Silence,
        Stopped
    }

    public enum SeekMode
    {
        Position,
        Forward,
        Backward
    }

    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;

    private readonly byte[] _buffer;
    private readonly Bytes _bufferSize;
    private readonly TimeSpan _bufferTime;
    private readonly CancellationTokenSource _cts;
    private readonly byte[] _emptyBuffer;
    private readonly string _id;
    private readonly Lock _lock;
    private readonly ILogger _logger;

    private readonly Stream _inputStream;
    private readonly Stream _outputStream;

    private AudioStream(
        Stream inputStream,
        Stream outputStream,
        ILogger logger,
        IOptions<AudioOptions> options,
        CancellationToken ct)
    {
        _id = Guid.NewGuid().ToString("N");
        _inputStream = inputStream;
        _outputStream = outputStream;
        _logger = logger;
        _bufferTime = options.Value.BufferTime;
        _bufferSize = Bytes.ToBytes(_bufferTime);
        _buffer = new byte[_bufferSize];
        _emptyBuffer = new byte[_bufferSize];
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _lock = new Lock();

        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    _logger.LogPosition(Position.HummanizeMillisecond(), Length.HummanizeMillisecond());

                    switch (State)
                    {
                        case AudioState.Playing:
                            await HandlePlayingAsync(_cts.Token);
                            break;
                        case AudioState.Silence:
                            await HandleSilenceAsync(_cts.Token);
                            break;
                        case AudioState.Stopped:
                            await HandleStoppedAsync(_cts.Token);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(State), State, $"Unknown state {State}");
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
        }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public AudioState State { get; private set; } = AudioState.Playing;
    public TimeSpan Length => Bytes.From(_inputStream.Length).ToTimeSpan();
    public TimeSpan Position => Bytes.From(_inputStream.Position).ToTimeSpan();

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        State = AudioState.Stopped;
        _cts.Cancel();
        StreamEnded = null;
        StreamFailed = null;
        _inputStream.Dispose();
    }

    private async Task HandlePlayingAsync(CancellationToken ct)
    {
        var bytesRead = await _inputStream.ReadAsync(_buffer, ct);

        if (bytesRead == 0)
        {
            State = AudioState.Stopped;
            _logger.LogStreamEnded(_id);

            if (StreamEnded is not null)
            {
                await StreamEnded(this, EventArgs.Empty);
            }

            return;
        }

        if (bytesRead < _bufferSize)
        {
            _logger.LogFilled(_id);
            _buffer.AsSpan(bytesRead).Clear();
        }

        _logger.LogStreaming(_id, "audio", _bufferSize.Value.Bytes().Humanize());
        await _outputStream.WriteAsync(_buffer, ct);
    }

    private async Task HandleSilenceAsync(CancellationToken ct)
    {
        _logger.LogStreaming(_id, "silence", _bufferSize.Value.Bytes().Humanize());
        await _outputStream.WriteAsync(_emptyBuffer, ct);
    }

    private async Task HandleStoppedAsync(CancellationToken ct)
    {
        await Task.Delay(_bufferTime, ct);
    }
    
    public event Func<Exception, object, EventArgs, Task>? StreamFailed;
    public event Func<object, EventArgs, Task>? StreamEnded;

    public void Pause()
    {
        lock (_lock)
        {
            State = AudioState.Silence;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            State = AudioState.Playing;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            State = AudioState.Stopped;
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
                _ => throw new ArgumentOutOfRangeException(nameof(seekMode), seekMode, null)
            };

            if (seekPosition > _inputStream.Length)
            {
                seekPosition = Bytes.From(_inputStream.Length);
            }

            if (seekPosition < 0)
            {
                seekPosition = Bytes.From(0);
            }

            State = AudioState.Playing;

            _inputStream.Position = seekPosition;
            return seekPosition.ToTimeSpan();
        }
    }

    public static ErrorOr<AudioStream> Load(IFileInfo audioFile, Stream outputStream,
        IFileSystem fileSystem, ILogger logger, IOptions<AudioOptions> options, CancellationToken ct)
    {
        if (!fileSystem.File.Exists(audioFile.FullName))
        {
            return Error.NotFound(description: $"Audio file '{audioFile.FullName}' does not exist");
        }
        
        var stream = new FileStream(audioFile.FullName, FileMode.Open, FileAccess.Read);

        logger.LogTrace("Audio stream loaded from {AudioFile} with {Length} bytes and duration {Duration}",
            audioFile.FullName, stream.Length.Bytes(),
            Bytes.From(stream.Length).ToTimeSpan().HummanizeMillisecond());

        return new AudioStream(stream, outputStream, logger, options, ct);
    }

    private class Bytes : ValueOf<long, Bytes>
    {
        public TimeSpan ToTimeSpan()
        {
            return TimeSpan.FromSeconds(1d * Value / (SampleRate * Channels * BytesPerSample));
        }

        public static Bytes ToBytes(TimeSpan time)
        {
            return From((long)Math.Ceiling(1d * time.TotalSeconds * SampleRate * Channels * BytesPerSample));
        }

        public static Bytes operator +(Bytes a, Bytes b)
        {
            return From(a.Value + b.Value);
        }

        public static Bytes operator -(Bytes a, Bytes b)
        {
            return From(a.Value - b.Value);
        }

        public static bool operator >(Bytes a, long b)
        {
            return a.Value > b;
        }

        public static bool operator <(Bytes a, long b)
        {
            return a.Value < b;
        }

        public static implicit operator long(Bytes bytes)
        {
            return bytes.Value;
        }
    }
}
