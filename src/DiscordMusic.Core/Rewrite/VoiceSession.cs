using NetCord;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Rewrite;

public record VoiceSession(VoiceClient VoiceClient, VoiceGuildChannel VoiceChannel, OpusEncodeStream OutputStream) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await OutputStream.DisposeAsync();
        await VoiceClient.CloseAsync();
    }
}
