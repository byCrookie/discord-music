using Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class PingCommand(ILogger<PingCommand> logger) : ModuleBase<SocketCommandContext>
{
    [UsedImplicitly]
    [Command("ping")]
    public Task PingAsync()
    {
        logger.LogTrace("Command ping");
        logger.LogDebug("Pong!");
        return ReplyAsync("pong!");
    }
}
