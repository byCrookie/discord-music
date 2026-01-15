using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.Voice;

public record VoiceConnection(
    VoiceClient VoiceClient,
    ulong GuildId,
    ulong ChannelId,
    Stream Output
) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        await Output.DisposeAsync();
        await VoiceClient.CloseAsync(cancellationToken: ct);
    }
}
