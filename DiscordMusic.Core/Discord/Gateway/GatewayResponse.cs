using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway;

internal sealed class GatewayResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("shards")]
    public int Shards { get; set; }
    
    [JsonPropertyName("session_start_limit")]
    public SessionStartLimitResponse SessionStartLimit { get; set; }

    public GatewayResponse(string url, int shards, SessionStartLimitResponse sessionStartLimit)
    {
        Url = url;
        Shards = shards;
        SessionStartLimit = sessionStartLimit;
    }
}