using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class IdentifyEventPresence(Activity[] activities)
{
    [JsonPropertyName("since")]
    public int? Since { get; set; }

    [JsonPropertyName("activities")]
    public Activity[] Activities { get; set; } = activities;

    [JsonPropertyName("status")]
    public PresenceUpdateStatus Status { get; set; }
    
    [JsonPropertyName("afk")]
    public bool Afk { get; set; }
}