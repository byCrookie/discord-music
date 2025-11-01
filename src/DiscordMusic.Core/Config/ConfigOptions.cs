using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Config;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class ConfigOptions
{
    public const string ConfigFileKey = "CONFIG_FILE";

    [ConfigurationKeyName(ConfigFileKey)]
    public string? ConfigFile { get; init; }
}
