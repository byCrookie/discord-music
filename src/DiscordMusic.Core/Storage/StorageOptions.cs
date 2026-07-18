using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Storage;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class StorageOptions
{
    public const string SectionName = "storage";

    [ConfigurationKeyName("maxSize")]
    public required string MaxSize { get; init; } = "5GB";

    [ConfigurationKeyName("path")]
    public string? Path { get; init; }
}
