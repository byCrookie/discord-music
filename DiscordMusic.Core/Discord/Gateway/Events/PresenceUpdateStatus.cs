using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public enum PresenceUpdateStatus
{
    [JsonPropertyName("online")]
    Online,
    [JsonPropertyName("dnd")]
    DoNotDisturb,
    [JsonPropertyName("idle")]
    Idle,
    [JsonPropertyName("invisible")]
    Invisible,
    [JsonPropertyName("offline")]
    Offline
}