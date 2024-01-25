using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class IdentifyEvent(
    string token,
    IdentifyEventProperties properties,
    bool? compress,
    int? largeThreshold,
    int[]? shard,
    IdentifyEventPresence? presence,
    GatewayIntents intents)
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = token;

    [JsonPropertyName("properties")]
    public IdentifyEventProperties Properties { get; set; } = properties;

    [JsonPropertyName("compress")]
    public bool? Compress { get; set; } = compress;

    [JsonPropertyName("large_threshold")]
    public int? LargeThreshold { get; set; } = largeThreshold;

    [JsonPropertyName("shard")]
    public int[]? Shard { get; set; } = shard;

    [JsonPropertyName("presence")]
    public IdentifyEventPresence? Presence { get; set; } = presence;

    [JsonPropertyName("intents")]
    public GatewayIntents Intents { get; set; } = intents;
}