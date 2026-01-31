using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Discord.VoiceCommands;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class JoinAction(
    GuildSessionManager guildSessionManager,
    ILogger<JoinAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("join", "The bot will join the voice channel you are in.")]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Join(
        [SlashCommandParameter(
            Description = "Enable voice commands. Bot listens for verbal commands. Default is no."
        )]
            VoiceCommandSetting listen = VoiceCommandSetting.No
    )
    {
        logger.LogTrace("Join");

        await guildSessionManager.LeaveAsync(Context.Guild!.Id, cancellation.CancellationToken);

        var session = await guildSessionManager.JoinAsync(
            Context,
            listen,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Connected
                    I joined your voice channel.
                    -# {ToJoinedString(listen)}
                    """,
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }

    private static string ToJoinedString(VoiceCommandSetting voiceCommandSetting) =>
        voiceCommandSetting switch
        {
            VoiceCommandSetting.Yes => "Voice commands are enabled",
            VoiceCommandSetting.No => "Voice commands are disabled",
            _ => "",
        };
}
