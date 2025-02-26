using DiscordMusic.Core.Audio;
using ErrorOr;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Voice;

public interface IVoiceHost
{
    Task<ErrorOr<Success>> ConnectAsync(Message message, CancellationToken ct);
    Task<ErrorOr<Success>> DisconnectAsync(CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> PlayAsync(Message message, string query, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> PlayNextAsync(Message message, string query, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> PauseAsync(Message message, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> ResumeAsync(Message message, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> NowPlayingAsync(Message message, CancellationToken ct);
    Task<ErrorOr<ICollection<Track>>> QueueAsync(Message message, CancellationToken ct);
    Task<ErrorOr<Success>> QueueClearAsync(Message message, CancellationToken ct);

    Task<ErrorOr<VoiceUpdate>> SeekAsync(
        Message message,
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    );

    Task<ErrorOr<VoiceUpdate>> ShuffleAsync(Message message, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> SkipAsync(Message message, int toIndex, CancellationToken ct);
}
