using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class HelloEvent
{
    [JsonPropertyName("heartbeat_interval")]
    public int HeartbeatInterval { get; set; }
}