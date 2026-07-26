using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal sealed class NowPlayingAction(
    ILogger<NowPlayingAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    TimeProvider timeProvider
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
        return DiscordMusicObservability.TrackDiscordCommand(
            "nowplaying",
            Context.Guild?.Id,
            Context.User.Id,
            timeProvider,
            activity =>
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
                    return DiscordMusicObservability.CommandResult(error, "missing_session");
                }

                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.Public(AudioBarRenderer.Render(session.Snapshot()))
                );
            }
        );
    }
}
