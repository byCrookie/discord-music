using DiscordMusic.Core.Audio;
using NetCord;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.Sessions;

public record GuildVoiceSession(
    VoiceClient VoiceClient,
    VoiceGuildChannel VoiceChannel,
    OpusEncodeStream OutputStream,
    AudioPlayer AudioPlayer
) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        AudioPlayer.Dispose();
        await OutputStream.DisposeAsync();
        await VoiceClient.CloseAsync();
        VoiceClient.Dispose();
    }
}
