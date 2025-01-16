using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.YouTube;

public readonly record struct YouTubeTrack(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("original_url")] [property: JsonConverter(typeof(FlurlUrlJsonConverter))] Url Url
);
