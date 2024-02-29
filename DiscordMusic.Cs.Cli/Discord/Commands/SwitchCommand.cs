using Discord.Commands;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

internal class SwitchCommand(ILogger<ListenCommand> logger, IState state) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("switch")]
    [Alias("s")]
    public async Task HelpAsync()
    {
        logger.LogTrace("Command switch");
        state.IsPaused = !state.IsPaused;

        if (state.IsPaused)
        {
            logger.LogTrace("Switched to paused");
            await ReplyAsync("Switched to paused");
        }
        else
        {
            logger.LogTrace("Switched to playing");
            await ReplyAsync("Switched to playing");
        }
    }
}
