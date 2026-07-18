using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.Tracks;

public record Track(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")] string Artists,
    [property: JsonPropertyName("url")]
    [property: JsonConverter(typeof(FlurlUrlJsonConverter))]
        Url Url,
    [property: JsonPropertyName("duration")] TimeSpan Duration
);
