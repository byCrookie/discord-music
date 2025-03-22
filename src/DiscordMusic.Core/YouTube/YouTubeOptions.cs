using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.YouTube;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class YouTubeOptions
{
    public const string SectionName = "youtube";

    [ConfigurationKeyName("ytdlp")]
    public string? Ytdlp { get; init; }

    [ConfigurationKeyName("ffmpeg")]
    public string? Ffmpeg { get; init; }
}
