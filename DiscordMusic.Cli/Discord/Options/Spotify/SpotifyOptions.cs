using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Cli.Discord.Options.Spotify;

public class SpotifyOptions
{
    public const string SectionName = "spotify";

    [ConfigurationKeyName("clientId")]
    public string ClientId { get; [UsedImplicitly] init; } = null!;

    [ConfigurationKeyName("clientSecret")]
    public string ClientSecret { get; [UsedImplicitly] init; } = null!;
}
