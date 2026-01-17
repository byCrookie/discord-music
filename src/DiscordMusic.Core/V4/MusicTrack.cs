using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.V4;

public record MusicTrack(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")] string Artists,
    [property: JsonPropertyName("url")]
    [property: JsonConverter(typeof(FlurlUrlJsonConverter))]
    Url Url,
    [property: JsonPropertyName("duration")] TimeSpan Duration
);

