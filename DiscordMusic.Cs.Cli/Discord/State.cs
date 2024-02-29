namespace DiscordMusic.Cs.Cli.Discord;

internal class State : IState
{
    public bool PlayOnFreeze { get; set; } = true;
    public bool Listen { get; set; } = true;
    public bool IsPaused { get; set; } = false;
}
