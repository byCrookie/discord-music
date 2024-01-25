using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class VoiceStateUpdate
{
    [JsonPropertyName("guild_id")]
    public string GuildId { get; set; } = null!;
    
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }
    
    [JsonPropertyName("self_deaf")]
    public bool SelfDeaf { get; set; }
    
    [JsonPropertyName("self_mute")]
    public bool SelfMute { get; set; }
}