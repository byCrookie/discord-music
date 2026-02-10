using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Audio;

public class AudioPlayer(
    ILogger<AudioPlayer> logger,
    ILogger<AudioStream> audioStreamLogger,
    IFileSystem fileSystem,
    Stream output
) : IDisposable
{
    private readonly AsyncLock _lock = new();
    private AudioStream? _audioStream;

    public async Task<ErrorOr<AudioStatus>> ResumeAsync(CancellationToken ct)
    {
        logger.LogTrace("Resuming audio");
        await using var _ = await _lock.AcquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.NotFound(description: "Nothing is playing right now.");
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
        await using var _ = await _lock.AcquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.NotFound(description: "Nothing is playing right now.");
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
        await using var _ = await _lock.AcquireAsync(ct);
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
        await using var _ = await _lock.AcquireAsync(ct);
        return _audioStream?.State == AudioStream.AudioState.Playing;
    }

    public async Task<ErrorOr<AudioStatus>> PauseAsync(CancellationToken ct)
    {
        logger.LogTrace("Pause audio");
        await using var _ = await _lock.AcquireAsync(ct);

        if (_audioStream is null)
        {
            return Error.NotFound(description: "Nothing is playing right now.");
        }

        _audioStream.Pause();
        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
    }

    public async Task<ErrorOr<AudioStatus>> PlayAsync(
        IFileInfo file,
        Func<AudioEvent, Exception?, CancellationToken, Task> updateAsync,
        CancellationToken ct
    )
    {
        logger.LogTrace("Play audio from file");
        await using var _ = await _lock.AcquireAsync(ct);

        var audioStream = AudioStream.Load(
            AudioStream.AudioState.Playing,
            file,
            output,
            fileSystem,
            audioStreamLogger,
            ct
        );

        if (audioStream.IsError)
        {
            return audioStream.Errors;
        }

        _audioStream?.Dispose();
        _audioStream = audioStream.Value;

        // Don't capture the caller's cancellation token here.
        // These callbacks can fire long after PlayAsync returned (e.g. natural end of a track).
        _audioStream.StreamEnded += async (_, _) =>
        {
            await output.FlushAsync(CancellationToken.None);
            updateAsync(AudioEvent.Ended, null, CancellationToken.None).FireAndForget(logger);
        };

        _audioStream.StreamFailed += async (e, _, _) =>
        {
            await output.FlushAsync(CancellationToken.None);
            updateAsync(AudioEvent.Error, e, CancellationToken.None).FireAndForget(logger);
        };

        return new AudioStatus(
            ToAudioState(_audioStream.State),
            _audioStream.Position,
            _audioStream.Length
        );
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

    public void Dispose()
    {
        _audioStream?.Dispose();
    }
}
