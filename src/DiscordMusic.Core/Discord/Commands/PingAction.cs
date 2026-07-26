using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal sealed class PingAction(ILogger<PingAction> logger, TimeProvider timeProvider)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "ping",
        "Bot will answer with Pong!",
        Contexts = [InteractionContextType.Guild, InteractionContextType.BotDMChannel]
    )]
    public Task<InteractionMessageProperties> Ping()
    {
        return Task.FromResult(
            DiscordMusicObservability.TrackDiscordCommand(
                "ping",
                Context.Guild?.Id,
                Context.User.Id,
                timeProvider,
                _ =>
                {
                    logger.LogTrace("Ping");
                    return DiscordMusicObservability.CommandResult(
                        DiscordResponses.Ephemeral("Pong!")
                    );
                }
            )
        );
    }
}
