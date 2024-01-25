using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Channel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("type")]
    public int Type { get; set; }
    
    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("recipients")]
    public User[] Recipients { get; set; } = Array.Empty<User>();
    
    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }
    
    [JsonPropertyName("flags")]
    public int? Flags { get; set; }
}