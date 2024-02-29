using Discord.Commands;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

internal class PlayOnFreezeCommand(ILogger<ListenCommand> logger, IState state) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("playOnFreeze")]
    [Alias("pof")]
    public async Task HelpAsync()
    {
        logger.LogTrace("Command play on freeze");
        state.PlayOnFreeze = !state.PlayOnFreeze;

        if (state.PlayOnFreeze)
        {
            logger.LogTrace("Play during freeze time");
            await ReplyAsync("Play during freeze time");
        }
        else
        {
            logger.LogTrace("Don't play during freeze time");
            await ReplyAsync("Don't play during freeze time");
        }
    }
}
