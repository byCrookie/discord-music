using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class ResumeAction(
    ILogger<ResumeAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    IPlaybackController playbackController
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("resume", "Resume paused playback.", Contexts = [InteractionContextType.Guild])]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties Resume()
    {
        return DiscordMusicObservability.TrackDiscordCommand(
            "resume",
            Context.Guild?.Id,
            Context.User.Id,
            activity =>
            {
                logger.LogTrace("Resume");

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

                var result = playbackController.Resume(session);
                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.PlaybackFeedback(result, session),
                    result.IsSuccess ? "completed" : "playback_rejected"
                );
            }
        );
    }
}
