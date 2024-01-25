using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class GuildMember
{
    [JsonPropertyName("guild_id")]
    public string GuildId { get; set; } = null!;
    
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }
}