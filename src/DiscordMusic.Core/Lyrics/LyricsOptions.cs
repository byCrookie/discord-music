using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Lyrics;

internal class LyricsOptions
{
    public const string SectionName = "lyrics";

    [ConfigurationKeyName("token")]
    public string Token { get; init; } = null!;
}
