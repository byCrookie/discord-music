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

    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;

    private readonly byte[] _buffer;
    private readonly Bytes _bufferSize;
    private readonly TimeSpan _bufferTime;
    private readonly CancellationTokenSource _cts;
    private readonly byte[] _emptyBuffer;
    private readonly string _id;
    private readonly Lock _lock;
    private readonly ILogger _logger;

    private readonly MemoryStream _memoryStream;
    private readonly Stream _outputStream;

    private AudioStream(
        MemoryStream memoryStream,
        Stream outputStream,
        ILogger logger,
        IOptions<AudioOptions> options,
        CancellationToken ct)
    {
        _id = Guid.NewGuid().ToString("N");
        _memoryStream = memoryStream;
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
        }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public AudioState State { get; private set; } = AudioState.Playing;
    public TimeSpan Length => Bytes.From(_memoryStream.Length).ToTimeSpan();
    public TimeSpan Position => Bytes.From(_memoryStream.Position).ToTimeSpan();

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        State = AudioState.Stopped;
        _cts.Cancel();
        StreamEnded = null;
        _memoryStream.Dispose();
    }

    private async Task HandlePlayingAsync(CancellationToken ct)
    {
        var bytesRead = await _memoryStream.ReadAsync(_buffer, ct);

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
                SeekMode.Forward => Bytes.From(_memoryStream.Position) + seekBytes,
                SeekMode.Backward => Bytes.From(_memoryStream.Position) - seekBytes,
                _ => throw new ArgumentOutOfRangeException(nameof(seekMode), seekMode, null)
            };

            if (seekPosition > _memoryStream.Length)
            {
                seekPosition = Bytes.From(_memoryStream.Length);
            }

            if (seekPosition < 0)
            {
                seekPosition = Bytes.From(0);
            }

            State = AudioState.Playing;

            _memoryStream.Position = seekPosition;
            return seekPosition.ToTimeSpan();
        }
    }

    public static async Task<AudioStream> LoadAsync(Stream audioStream, Stream outputStream,
        ILogger logger, IOptions<AudioOptions> options, CancellationToken ct)
    {
        var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        logger.LogTrace("Audio stream loaded from stream with {Length} bytes and duration {Duration}",
            memoryStream.Length.Bytes(), Bytes.From(memoryStream.Length).ToTimeSpan());

        return new AudioStream(memoryStream, outputStream, logger, options, ct);
    }

    public static async Task<ErrorOr<AudioStream>> LoadAsync(IFileInfo audioFile, Stream outputStream,
        IFileSystem fileSystem,
        ILogger logger, IOptions<AudioOptions> options, CancellationToken ct)
    {
        if (!fileSystem.File.Exists(audioFile.FullName))
        {
            return Error.NotFound(description: $"Audio file '{audioFile.FullName}' does not exist");
        }

        var memoryStream = new MemoryStream();
        var ffmpegArgs = $"-i \"{audioFile.FullName}\" -f s{BitsPerSample}le -ar {SampleRate} -ac {Channels} pipe:1";
        logger.LogTrace("Calling {Ffmpeg} with arguments {FfmpegArgs}", options.Value.Ffmpeg, ffmpegArgs);
        using var ffmpeg = Process.Start(
            new ProcessStartInfo
            {
                FileName = options.Value.Ffmpeg,
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        );

        if (ffmpeg is null)
        {
            return Error.Unexpected(
                description: $"Process {options.Value.Ffmpeg} with arguments {ffmpegArgs} failed to start");
        }

        await using var ffmpegStream = ffmpeg.StandardOutput.BaseStream;
        await ffmpegStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        logger.LogTrace("Audio stream loaded from {AudioFile} with {Length} bytes and duration {Duration}",
            audioFile.FullName, memoryStream.Length.Bytes(),
            Bytes.From(memoryStream.Length).ToTimeSpan().HummanizeMillisecond());

        return new AudioStream(memoryStream, outputStream, logger, options, ct);
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
