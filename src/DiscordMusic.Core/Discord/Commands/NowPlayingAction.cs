using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class NowPlayingAction(
    ILogger<NowPlayingAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "nowplaying",
        "Show the currently playing track.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    public InteractionMessageProperties NowPlaying()
    {
        logger.LogTrace("NowPlaying");

        if (
            !VoiceCommandGuard.TryGetPlaybackSession(
                Context,
                voiceInstances,
                playbackService,
                out var session,
                out _,
                out var error
            )
        )
        {
            return error;
        }

        return DiscordResponses.Public(AudioBarRenderer.Render(session.Snapshot()));
    }
}
