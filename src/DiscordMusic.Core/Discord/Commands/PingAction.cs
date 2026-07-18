using DiscordMusic.Core.Discord.CommandSupport;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class PingAction(ILogger<PingAction> logger)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "ping",
        "Bot will answer with Pong!",
        Contexts = [InteractionContextType.Guild, InteractionContextType.BotDMChannel]
    )]
    public Task<InteractionMessageProperties> Ping()
    {
        logger.LogTrace("Ping");
        return Task.FromResult(DiscordResponses.Ephemeral("Pong!"));
    }
}
