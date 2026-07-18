using System.Text.Json.Serialization;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using Flurl;

namespace DiscordMusic.Core.YouTube.Searching;

public readonly record struct YouTubeTrack(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("original_url")]
    [property: JsonConverter(typeof(FlurlUrlJsonConverter))]
        Url Url
)
{
    public Track ToTrack()
    {
        return new Track(
            Hash.Sha256(Url.ToString()),
            Title,
            Channel,
            Url,
            Duration is null ? TimeSpan.Zero : TimeSpan.FromSeconds(Duration.Value)
        );
    }
}
