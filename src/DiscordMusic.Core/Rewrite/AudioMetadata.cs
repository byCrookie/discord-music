using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.Rewrite;

public record AudioMetadata(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artists")] string Artists,
    [property: JsonPropertyName("url")]
    [property: JsonConverter(typeof(FlurlUrlJsonConverter))]
        Url Url,
    [property: JsonPropertyName("duration")] TimeSpan Duration
);
