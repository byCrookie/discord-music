using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Integration
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    
    [JsonPropertyName("syncing")]
    public bool Syncing { get; set; }
    
    [JsonPropertyName("role_id")]
    public string RoleId { get; set; } = null!;
    
    [JsonPropertyName("enable_emoticons")]
    public bool EnableEmoticons { get; set; }
    
    [JsonPropertyName("expire_behavior")]
    public int ExpireBehavior { get; set; }
    
    [JsonPropertyName("expire_grace_period")]
    public int ExpireGracePeriod { get; set; }
    
    [JsonPropertyName("user")]
    public User User { get; set; } = null!;
    
    [JsonPropertyName("account")]
    public Account Account { get; set; } = null!;
    
    [JsonPropertyName("synced_at")]
    public DateTime SyncedAt { get; set; }
    
    [JsonPropertyName("subscriber_count")]
    public int SubscriberCount { get; set; }
    
    [JsonPropertyName("revoked")]
    public bool Revoked { get; set; }
    
    [JsonPropertyName("application")]
    public Application Application { get; set; } = null!;
    
    [JsonPropertyName("scopes")]
    public string Scopes { get; set; } = null!;
}