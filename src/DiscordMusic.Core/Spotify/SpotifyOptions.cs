using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Spotify;

internal class SpotifyOptions
{
    public const string SectionName = "spotify";

    [ConfigurationKeyName("clientId")]
    public string ClientId { get; init; } = null!;

    [ConfigurationKeyName("clientSecret")]
    public string ClientSecret { get; init; } = null!;
}