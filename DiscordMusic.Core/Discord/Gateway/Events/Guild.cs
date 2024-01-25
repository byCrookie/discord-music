using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Guild
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("owner")]
    public bool? Owner { get; set; }
    
    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = null!;
    
    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }
}