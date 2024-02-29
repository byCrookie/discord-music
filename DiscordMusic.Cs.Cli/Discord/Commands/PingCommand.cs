using Discord.Commands;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

internal class PingCommand(ILogger<PingCommand> logger) : ModuleBase<CommandContext>
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
