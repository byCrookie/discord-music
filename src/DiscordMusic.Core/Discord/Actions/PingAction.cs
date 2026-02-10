using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class PingAction(ILogger<PingAction> logger) : SafeApplicationCommandModule
{
    [SlashCommand("ping", "Ping the bot. It will pong back.")]
    public async Task Ping()
    {
        logger.LogTrace("Ping");
        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = "Pong!",
                    Flags = MessageFlags.Ephemeral,
                }
            ),
            logger
        );
    }
}
