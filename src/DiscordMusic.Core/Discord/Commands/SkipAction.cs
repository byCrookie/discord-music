using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class SkipAction(
    ILogger<SkipAction> logger,
    VoiceConnectionRegistry voiceInstances,
    VoiceConnectionService voiceConnectionService,
    PlaybackService playbackService,
    IPlaybackController playbackController
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
        logger.LogTrace("Skip");

        if (!VoiceCommandGuard.TryGetGuild(Context, out var guildId, out var error))
        {
            return error;
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
                return DiscordResponses.Ephemeral(
                    $"{joinResult.Message} I can skip tracks only after joining a voice channel."
                );
            }

            session = joinResult.Connection.PlaybackSession;
        }
        else
        {
            return DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        }

        var result = (
            index is { } queueIndex
                ? await playbackController.SkipToAsync(
                    guildId,
                    session,
                    queueIndex,
                    CancellationToken.None
                )
                : await playbackController.SkipAsync(guildId, session, CancellationToken.None)
        );

        return DiscordResponses.FromPlaybackResult(result);
    }
}
