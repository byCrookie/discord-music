using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class StopAction(
    ILogger<StopAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    IPlaybackController playbackController
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "stop",
        "Stop playback and clear the queue.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Stop()
    {
        return DiscordMusicObservability.TrackDiscordCommand(
            "stop",
            Context.Guild?.Id,
            Context.User.Id,
            _ =>
            {
                logger.LogTrace("Stop");

                if (
                    !VoiceCommandGuard.TryGetPlaybackSession(
                        Context,
                        voiceInstances,
                        playbackService,
                        out var session,
                        out var guildId,
                        out var error
                    )
                )
                {
                    return DiscordMusicObservability.CommandResult(error, "missing_session");
                }

                var result = playbackController.Stop(guildId, session);
                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.FromPlaybackResult(result),
                    result.IsSuccess ? "completed" : "playback_rejected"
                );
            }
        );
    }
}
