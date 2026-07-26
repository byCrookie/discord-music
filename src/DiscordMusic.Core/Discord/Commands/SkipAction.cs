using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal sealed class SkipAction(
    ILogger<SkipAction> logger,
    VoiceConnectionRegistry voiceInstances,
    VoiceConnectionService voiceConnectionService,
    PlaybackService playbackService,
    IPlaybackController playbackController,
    TimeProvider timeProvider
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "skip",
        "Skip the currently playing track.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task<InteractionMessageProperties> Skip(
        [SlashCommandParameter(Description = "Optional 1-based queue index to skip to.")]
            int? index = null
    )
    {
        return await DiscordMusicObservability.TrackDiscordCommandAsync(
            index is null ? "skip" : "skip.to",
            Context.Guild?.Id,
            Context.User.Id,
            timeProvider,
            async _ =>
            {
                logger.LogTrace("Skip");

                if (!VoiceCommandGuard.TryGetGuild(Context, out var guildId, out var error))
                {
                    return DiscordMusicObservability.CommandResult(error, "missing_guild");
                }

                PlaybackSession session;
                if (
                    voiceInstances.Mapping.TryGetValue(guildId, out var voiceConnection)
                    && voiceConnection is not null
                )
                {
                    session = voiceConnection.PlaybackSession;
                }
                else if (playbackService.TryGetPlaybackSession(guildId, out session))
                {
                    logger.LogInformation(
                        "Resolved playback session from active playback loop after voice registry miss. GuildId={GuildId}",
                        guildId
                    );
                }
                else if (Context.Guild is { } guild)
                {
                    var joinResult = await voiceConnectionService.JoinUserChannelAsync(
                        Context.Client,
                        guildId,
                        guild.VoiceStates,
                        Context.User.Id
                    );

                    if (!joinResult.Succeeded || joinResult.Connection is null)
                    {
                        return DiscordMusicObservability.CommandResult(
                            DiscordResponses.Ephemeral(
                                $"{joinResult.Message} I can skip tracks only after joining a voice channel."
                            ),
                            "voice_join_failed"
                        );
                    }

                    session = joinResult.Connection.PlaybackSession;
                }
                else
                {
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral("The guild is not available. Try again later."),
                        "missing_guild"
                    );
                }

                var result = (
                    index is { } queueIndex
                        ? await playbackController.SkipToAsync(
                            guildId,
                            session,
                            queueIndex,
                            CancellationToken.None
                        )
                        : await playbackController.SkipAsync(
                            guildId,
                            session,
                            CancellationToken.None
                        )
                );

                return DiscordMusicObservability.CommandResult(
                    DiscordResponses.FromPlaybackResult(result),
                    result.IsSuccess ? "completed" : "playback_rejected"
                );
            }
        );
    }
}
