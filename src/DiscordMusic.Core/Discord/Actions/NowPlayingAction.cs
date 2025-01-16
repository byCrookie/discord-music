using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class NowPlayingAction(IVoiceHost voiceHost, IReplies replies, ILogger<NowPlayingAction> logger) : IDiscordAction
{
    public string Long => "nowplaying";
    public string Short => "np";

    public string Help =>
        """
        Shows the currently playing track
        Usage: `nowplaying`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Nowplaying");
        var nowPlaying = await voiceHost.NowPlayingAsync(message, ct);

        if (nowPlaying.IsError)
        {
            return nowPlaying.Errors;
        }

        if (nowPlaying.Value.Track is null)
        {
            await replies.SendAsync(message, "Now playing", "No track is currently playing", ct);
            return Result.Success;
        }

        var track = nowPlaying.Value.Track;

        var nowPlayingMessage = $"""
                                 **{track.Name}** by **{track.Artists}**
                                 {nowPlaying.Value.AudioStatus.Position.HummanizeSecond()} / {nowPlaying.Value.AudioStatus.Length.HummanizeSecond()}
                                 """;

        await replies.SendWithDeletionAsync(message, "Now", nowPlayingMessage, IReplies.DefaultDeletionDelay, ct);
        return Result.Success;
    }
}
