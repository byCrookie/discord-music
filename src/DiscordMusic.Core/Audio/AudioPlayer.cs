using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Audio;

public class AudioPlayer(
    ILogger<AudioPlayer> logger,
    ILogger<AudioStream> audioStreamlogger,
    IOptions<AudioOptions> options,
    IFileSystem fileSystem
) : IAudioPlayer
{
    private readonly AsyncLock _lock = new();
    private AudioStream? _audioStream;
    private Stream? _output;
    private Func<AudioEvent, Exception?, CancellationToken, Task>? _updateAsync;

    public async Task<ErrorOr<AudioStatus>> ResumeAsync(CancellationToken ct)
    {
        logger.LogTrace("Resuming audio");
        await using var _ = await _lock.AquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.NotFound(description: "No audio to resume");
        }

        _audioStream.Resume();
        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
    }

    public async Task<ErrorOr<AudioStatus>> SeekAsync(
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    )
    {
        logger.LogTrace("Seek audio");
        await using var _ = await _lock.AquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.Unexpected(description: "No audio to seek");
        }

        var seek = _audioStream.Seek(time, mode);

        if (seek.IsError)
        {
            return seek.Errors;
        }

        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
    }

    public async Task<AudioStatus> StatusAsync(CancellationToken ct)
    {
        logger.LogTrace("Status of audio");
        await using var _ = await _lock.AquireAsync(ct);
        return _audioStream is null
            ? AudioStatus.Stopped
            : new AudioStatus(
                ToAudioState(_audioStream.State),
                _audioStream.Position,
                _audioStream.Length
            );
    }

    public async Task<bool> IsPlayingAsync(CancellationToken ct)
    {
        logger.LogTrace("Is audio playing");
        await using var _ = await _lock.AquireAsync(ct);
        return _audioStream?.State == AudioStream.AudioState.Playing;
    }

    public async Task<ErrorOr<AudioStatus>> PauseAsync(CancellationToken ct)
    {
        logger.LogTrace("Pause audio");
        await using var _ = await _lock.AquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.Unexpected(description: "No audio to pause");
        }

        _audioStream.Pause();
        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
    }

    public async Task StartAsync(
        Stream output,
        Func<AudioEvent, Exception?, CancellationToken, Task> updateAsync,
        CancellationToken ct
    )
    {
        logger.LogTrace("Start audio");
        await using var l = await _lock.AquireAsync(ct);
        _output = output;
        _updateAsync = updateAsync;
    }

    public async Task<ErrorOr<AudioStatus>> PlayAsync(IFileInfo file, CancellationToken ct)
    {
        logger.LogTrace("Play audio from file");
        await using var _ = await _lock.AquireAsync(ct);

        if (_output is null || _updateAsync is null)
        {
            return Error.Unexpected(description: "Audio not started");
        }

        var audioStream = AudioStream.Load(
            file,
            _output,
            fileSystem,
            audioStreamlogger,
            options,
            ct
        );

        if (audioStream.IsError)
        {
            return audioStream.Errors;
        }

        _audioStream?.Dispose();
        _audioStream = audioStream.Value;

        _audioStream.StreamEnded += async (_, _) =>
        {
            await _output.FlushAsync(ct);
            _updateAsync(AudioEvent.Ended, null, ct).FireAndForget(logger, ct);
        };

        _audioStream.StreamFailed += async (e, _, _) =>
        {
            await _output.FlushAsync(ct);
            _updateAsync(AudioEvent.Error, e, ct).FireAndForget(logger, ct);
        };

        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
    }

    public async Task StopAsync(CancellationToken ct)
    {
        logger.LogTrace("Stop audio");
        await using var _ = await _lock.AquireAsync(ct);
        _audioStream?.Dispose();
        _audioStream = null;
        _output = null;
        _updateAsync = null;
    }

    private static AudioState ToAudioState(AudioStream.AudioState state)
    {
        return state switch
        {
            AudioStream.AudioState.Playing => AudioState.Playing,
            AudioStream.AudioState.Silence => AudioState.Paused,
            AudioStream.AudioState.Ended => AudioState.Ended,
            _ => AudioState.Stopped,
        };
    }
}
