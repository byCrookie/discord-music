using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class SeekAction(
    ILogger<SeekAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    IPlaybackController playbackController
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "seek",
        "Seek in the currently playing track.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Seek(
        [SlashCommandParameter(Description = "Target position, e.g. 90, 1:30, or 00:01:30.")]
            string position
    )
    {
        return DiscordMusicObservability.TrackDiscordCommand(
            "seek",
            Context.Guild?.Id,
            Context.User.Id,
            activity =>
            {
                logger.LogTrace("Seek");

                if (
                    !TimeSpanParser.TryParse(position, out var targetPosition)
                    || targetPosition < TimeSpan.Zero
                )
                {
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral("Invalid seek position."),
                        "invalid_position"
                    );
                }

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

                var result = playbackController.Seek(session, targetPosition);
                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.PlaybackFeedback(
                        result,
                        session,
                        result.IsSuccess ? targetPosition : null
                    ),
                    result.IsSuccess ? "completed" : "playback_rejected"
                );
            }
        );
    }
}
