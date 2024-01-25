namespace DiscordMusic.Cli.Utils;

public record Separators(string[] Values)
{
    public override string ToString()
    {
        return string.Join(" or ", Values);
    }
};