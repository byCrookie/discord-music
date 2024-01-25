using System.Net.Mime;
using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class ReadyEvent
{
    public ReadyEvent(int version, User user, UnavailableGuild[] guilds, string sessionId, string resumeGatewayUrl, int[]? shard,
        Application application)
    {
        Version = version;
        User = user;
        Guilds = guilds;
        SessionId = sessionId;
        ResumeGatewayUrl = resumeGatewayUrl;
        Shard = shard;
        Application = application;
    }

    [JsonPropertyName("v")]
    public int Version { get; set; }

    [JsonPropertyName("user")]
    public User User { get; set; }

    [JsonPropertyName("guilds")]
    public UnavailableGuild[] Guilds { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; }

    [JsonPropertyName("resume_gateway_url")]
    public string ResumeGatewayUrl { get; set; }

    [JsonPropertyName("shard")]
    public int[]? Shard { get; set; }

    [JsonPropertyName("application")]
    public Application Application { get; set; }
}