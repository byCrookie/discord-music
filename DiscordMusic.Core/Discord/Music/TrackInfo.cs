using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DiscordMusic.Core.Discord.Music;

[UsedImplicitly]
internal record TrackInfo(
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("channel")]
    string Channel,
    [property: JsonPropertyName("duration")]
    double Duration,
    [property: JsonPropertyName("original_url")]
    string Url
);