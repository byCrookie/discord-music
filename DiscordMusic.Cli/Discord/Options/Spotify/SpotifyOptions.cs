using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Cli.Discord.Options.Spotify;

public class SpotifyOptions
{
    public const string SectionName = "spotify";

    [Required]
    [ConfigurationKeyName("clientId")]
    public string ClientId { get; [UsedImplicitly] init; } = null!;

    [Required]
    [ConfigurationKeyName("clientSecret")]
    public string ClientSecret { get; [UsedImplicitly] init; } = null!;
}
