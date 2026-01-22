using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class PingAction(ILogger<SeekAction> logger)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Ping the bot. It will pong back.")]
    public async Task Ping()
    {
        logger.LogTrace("Ping");
        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = "Pong!",
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
