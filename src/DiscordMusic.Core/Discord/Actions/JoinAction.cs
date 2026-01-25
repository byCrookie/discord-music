using DiscordMusic.Core.Discord.Sessions;
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
    Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("join", "The bot will join the voice channel you are in.")]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Join([SlashCommandParameter(
            Description = "Enable voice commands."
        )]
        bool listen = true)
    {
        logger.LogTrace("Join");

        var session =
            await guildSessionManager.JoinAsync(Context, listen, cancellation.CancellationToken);

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToContent(),
                        Flags = MessageFlags.Ephemeral
                    }
                )
            );
            return;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = """
                              ### Joined
                              Joined your voice channel!
                              """,
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
