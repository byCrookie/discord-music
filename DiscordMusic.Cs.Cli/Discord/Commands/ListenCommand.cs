using Discord.Commands;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

internal class ListenCommand(ILogger<ListenCommand> logger, IState state) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("listen")]
    [Alias("l")]
    public async Task HelpAsync()
    {
        logger.LogTrace("Command listen");
        state.Listen = !state.Listen;

        if (state.Listen)
        {
            await ReplyAsync("I'm listening to cs now!");
        }
        else
        {
            await ReplyAsync("I'm not listening to cs anymore!");
        }
    }
}
