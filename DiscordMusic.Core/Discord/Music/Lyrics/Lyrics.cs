using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Music.Lyrics;

public record Lyrics(
    [property: JsonPropertyName("title")]
    string? Title,
    [property: JsonPropertyName("artist")]
    string? Artist,
    [property: JsonPropertyName("lyrics")]
    string? Text,
    [property: JsonPropertyName("source")]
    string? Source
);
