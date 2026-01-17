using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class LeaveAction(
    IVoiceHost voiceHost,
    ILogger<LeaveAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("leave", "The bot will leave the voice channel.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Leave()
    {
        logger.LogTrace("Leave");
        await voiceHost.DisconnectAsync(cancellation.CancellationToken);
        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = "### Left the voice channel.",
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
