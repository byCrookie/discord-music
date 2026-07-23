using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class JoinAction(
    ILogger<JoinAction> logger,
    VoiceConnectionService voiceConnectionService,
    IVoiceGuildChannel? channel = null
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "join",
        "Make the bot join your voice channel.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task<InteractionMessageProperties> Join()
    {
        return await DiscordMusicObservability.TrackDiscordCommandAsync(
            "join",
            Context.Guild?.Id,
            Context.User.Id,
            async _ =>
            {
                logger.LogTrace("Join");

                if (Context.Guild is not { } guild)
                {
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral("The guild is not available. Try again later."),
                        "missing_guild"
                    );
                }

                var user = Context.User;

                var result = await voiceConnectionService.JoinUserChannelAsync(
                    Context.Client,
                    guild.Id,
                    guild.VoiceStates,
                    user.Id,
                    channel?.Id
                );

                return DiscordMusicObservability.CommandResult(
                    result.Status == VoiceConnectionResultStatus.Connected
                        ? DiscordResponses.Ephemeral("Joined voice channel.")
                        : DiscordResponses.Ephemeral(result.Message),
                    result.Succeeded ? "completed" : "voice_join_failed"
                );
            }
        );
    }
}
