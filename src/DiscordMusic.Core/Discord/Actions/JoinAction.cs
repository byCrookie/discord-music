using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class JoinAction(IVoiceHost voiceHost, ILogger<JoinAction> logger, Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("join", "The bot will join the voice channel you are in.")]
    [RequireBotPermissions<ApplicationCommandContext>(
        Permissions.Connect | Permissions.PrioritySpeaker | Permissions.Speak
    )]
    [RequireUserPermissions<ApplicationCommandContext>(Permissions.Connect | Permissions.Speak)]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Join()
    {
        logger.LogTrace("Join");
        await voiceHost.ConnectAsync(Context, cancellation.CancellationToken);
        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = "### Joined your voice channel.",
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
