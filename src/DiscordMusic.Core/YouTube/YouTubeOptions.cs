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

    [ConfigurationKeyName("deno")]
    public string? Deno { get; init; }

    [ConfigurationKeyName("ffmpeg")]
    public string? Ffmpeg { get; init; }

    [ConfigurationKeyName("jsRuntimes")]
    public List<string> JsRuntimes { get; init; } = ["deno"];

    [ConfigurationKeyName("remoteComponents")]
    public List<string> RemoteComponents { get; init; } = ["ejs:github"];

    [ConfigurationKeyName("noJsRuntimes")]
    public bool NoJsRuntimes { get; init; }

    [ConfigurationKeyName("noRemoteComponents")]
    public bool NoRemoteComponents { get; init; }
}
