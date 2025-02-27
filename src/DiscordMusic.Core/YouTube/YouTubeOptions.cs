using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.YouTube;

public class YouTubeOptions
{
    public const string SectionName = "youtube";

    [ConfigurationKeyName("ytdlp")]
    public string Ytdlp { get; init; } = Directory.GetCurrentDirectory();

    [ConfigurationKeyName("ffmpeg")]
    public string Ffmpeg { get; init; } = Directory.GetCurrentDirectory();
}
