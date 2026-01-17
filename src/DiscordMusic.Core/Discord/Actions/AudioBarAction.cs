using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class AudioBarAction(ILogger<AudioBarAction> logger, Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("audiobar", "An audio bar with buttons to control the audio.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task AudioBar()
    {
        logger.LogTrace("Audio bar");

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties { Components = [Interactions.AudioBar.Create()] }
            ),
            cancellationToken: cancellation.CancellationToken
        );
    }
}
