using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NetCord;

namespace DiscordMusic.Core.Discord;

public class DiscordOptions
{
    public const string SectionName = "discord";

    [Required]
    [ConfigurationKeyName("applicationId")]
    public string ApplicationId { get; init; } = null!;

    [Required]
    [ConfigurationKeyName("token")]
    public string Token { get; init; } = null!;

    [ConfigurationKeyName("prefix")]
    public string Prefix { get; init; } = "!";

    [ConfigurationKeyName("roles")]
    public List<string> Roles { get; init; } = [];

    [ConfigurationKeyName("allow")]
    public List<string> Allow { get; init; } = [];

    [ConfigurationKeyName("deny")]
    public List<string> Deny { get; init; } = [];

    [ConfigurationKeyName("color")]
    public string Color { get; init; } = "000000";

    [JsonIgnore]
    public Color DiscordColor => new(int.Parse(Color, NumberStyles.HexNumber));
}
