namespace DiscordMusic.Cs.Cli.Discord;

public interface IState
{
    public bool PlayOnFreeze { get; set; }
    public bool Listen { get; set; }
    public bool IsPaused { get; set; }
}
