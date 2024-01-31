using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Music;

public record Track(
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("author")]
    string Author,
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("duration")]
    TimeSpan Duration,
    [property: JsonPropertyName("id")]
    Guid Id
);