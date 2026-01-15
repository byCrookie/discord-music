using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.Discord;

public record Track(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")] string Artists,
    [property: JsonPropertyName("url")]
    [property: JsonConverter(typeof(FlurlUrlJsonConverter))]
        Url Url,
    [property: JsonPropertyName("duration")] TimeSpan Duration
);
