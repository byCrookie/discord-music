using DiscordMusic.Cs.Cli.Discord.Options;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Discord;

internal class State(IOptions<CsOptions> options) : IState
{
    public bool PlayOnFreeze { get; set; } = options.Value.PlayOnFreeze;
    public bool Listen { get; set; } = options.Value.Listen;
    public bool IsPaused { get; set; } = options.Value.IsPaused;
}
