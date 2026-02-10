using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class AudioBarAction(ILogger<AudioBarAction> logger, Cancellation cancellation)
    : SafeApplicationCommandModule
{
    [SlashCommand("audiobar", "An audio bar with buttons to control the audio.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task AudioBar()
    {
        logger.LogTrace("Audio bar");

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties { Components = [Interactions.AudioBar.Create()] }
            ),
            logger,
            cancellation.CancellationToken
        );
    }
}
