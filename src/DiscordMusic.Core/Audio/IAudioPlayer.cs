using System.IO.Abstractions;
using ErrorOr;

namespace DiscordMusic.Core.Audio;

public interface IAudioPlayer
{
    Task StartAsync(Stream output, Func<AudioEvent, CancellationToken, Task> updateAsync, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task<ErrorOr<AudioStatus>> PlayAsync(Stream stream, CancellationToken ct);
    Task<ErrorOr<AudioStatus>> PlayAsync(IFileInfo file, CancellationToken ct);
    Task<ErrorOr<AudioStatus>> PauseAsync(CancellationToken ct);
    Task<ErrorOr<AudioStatus>> ResumeAsync(CancellationToken ct);
    Task<ErrorOr<AudioStatus>> SeekAsync(TimeSpan time, AudioStream.SeekMode mode, CancellationToken ct);
    Task<AudioStatus> StatusAsync(CancellationToken ct);
    Task<bool> IsPlayingAsync(CancellationToken ct);
}
