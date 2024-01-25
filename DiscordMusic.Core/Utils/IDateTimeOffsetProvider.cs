namespace DiscordMusic.Core.Utils;

public interface IDateTimeOffsetProvider
{
    public DateTimeOffset Now { get; }
    public DateTimeOffset UtcNow { get; }
}