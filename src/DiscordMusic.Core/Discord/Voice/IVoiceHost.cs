using DiscordMusic.Core.Audio;
using ErrorOr;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Voice;

public interface IVoiceHost
{
    event Func<VoiceReceiveEventArgs, ValueTask>? VoiceReceive;
    VoiceClient? VoiceClient { get; }
    VoiceConnection? VoiceConnection { get; }
    Task<ErrorOr<Success>> ConnectAsync(VoiceHostContext voiceHostContext, CancellationToken ct);
    Task<ErrorOr<Success>> DisconnectAsync(CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> PlayAsync(
        VoiceHostContext voiceHostContext,
        string query,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> PlayNextAsync(
        VoiceHostContext voiceHostContext,
        string query,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> PauseAsync(VoiceHostContext voiceHostContext, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> ResumeAsync(VoiceHostContext voiceHostContext, CancellationToken ct);
    Task<ErrorOr<VoiceUpdate>> NowPlayingAsync(
        VoiceHostContext voiceHostContext,
        CancellationToken ct
    );
    Task<ErrorOr<ICollection<Track>>> QueueAsync(
        VoiceHostContext voiceHostContext,
        CancellationToken ct
    );
    Task<ErrorOr<Success>> QueueClearAsync(VoiceHostContext voiceHostContext, CancellationToken ct);

    Task<ErrorOr<VoiceUpdate>> SeekAsync(
        VoiceHostContext voiceHostContext,
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    );

    Task<ErrorOr<VoiceUpdate>> ShuffleAsync(
        VoiceHostContext voiceHostContext,
        CancellationToken ct
    );
    Task<ErrorOr<VoiceUpdate>> SkipAsync(
        VoiceHostContext voiceHostContext,
        int toIndex,
        CancellationToken ct
    );
}
