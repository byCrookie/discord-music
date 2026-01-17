using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

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
}
