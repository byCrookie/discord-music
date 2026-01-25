using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Rewrite.Actions;

public class JoinAction(
    MusicSessionManager musicSessionManager,
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
            await musicSessionManager.JoinAsync(Context, listen, cancellation.CancellationToken);

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
