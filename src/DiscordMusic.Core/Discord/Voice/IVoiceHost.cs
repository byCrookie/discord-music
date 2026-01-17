using DiscordMusic.Core.Audio;
using ErrorOr;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Voice;

public interface IVoiceHost
{
    Task<ErrorOr<Success>> ConnectAsync(ApplicationCommandContext context, CancellationToken ct);
    Task<ErrorOr<Success>> DisconnectAsync(CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> PlayAsync(
        ApplicationCommandContext context,
        string query,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> PlayNextAsync(
        ApplicationCommandContext context,
        string query,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> PauseAsync(ApplicationCommandContext context, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> ResumeAsync(ApplicationCommandContext context, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> NowPlayingAsync(
        ApplicationCommandContext context,
        CancellationToken ct
    );
    Task<ErrorOr<ICollection<Track>>> QueueAsync(
        ApplicationCommandContext context,
        CancellationToken ct
    );
    Task<ErrorOr<Success>> QueueClearAsync(ApplicationCommandContext context, CancellationToken ct);

    Task<ErrorOr<VoiceUpdate>> SeekAsync(
        ApplicationCommandContext context,
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    );

    Task<ErrorOr<VoiceUpdate>> ShuffleAsync(
        ApplicationCommandContext context,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> SkipAsync(
        ApplicationCommandContext context,
        int toIndex,
        CancellationToken ct
    );
}
