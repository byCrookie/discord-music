using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Config;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
internal class CacheOptions
{
    public const string SectionName = "cache";

    [ConfigurationKeyName("maxSize")]
    public required string MaxSize { get; init; } = "5GB";

    [ConfigurationKeyName("location")]
    public string? Location { get; init; }
}
