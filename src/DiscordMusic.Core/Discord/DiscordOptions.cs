using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NetCord;

namespace DiscordMusic.Core.Discord;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class DiscordOptions
{
    public const string SectionName = "discord";

    [Required]
    [ConfigurationKeyName("token")]
    public required string Token { get; init; }

    [ConfigurationKeyName("prefix")]
    public string Prefix { get; init; } = "!";

    [ConfigurationKeyName("roles")]
    public List<string> Roles { get; init; } = ["DJ"];

    [ConfigurationKeyName("allow")]
    public List<string> Allow { get; init; } = ["music"];

    [ConfigurationKeyName("deny")]
    public List<string> Deny { get; init; } = [];

    [ConfigurationKeyName("color")]
    public string Color { get; init; } = "000000";

    [JsonIgnore]
    public Color DiscordColor => new(int.Parse(Color, NumberStyles.HexNumber));
}
