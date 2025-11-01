using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Spotify;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
internal class SpotifyOptions
{
    public const string SectionName = "spotify";

    [ConfigurationKeyName("clientId")]
    public string? ClientId { get; init; }

    [ConfigurationKeyName("clientSecret")]
    public string? ClientSecret { get; init; }
}
