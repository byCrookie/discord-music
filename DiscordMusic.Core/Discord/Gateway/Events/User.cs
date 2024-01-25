using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = null!;
    
    [JsonPropertyName("discriminator")]
    public string Discriminator { get; set; } = null!;
    
    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; } = null!;
    
    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
    
    [JsonPropertyName("bot")]
    public bool? Bot { get; set; }
    
    [JsonPropertyName("system")]
    public bool? System { get; set; }
    
    [JsonPropertyName("mfa_enabled")]
    public bool? MfaEnabled { get; set; }
    
    [JsonPropertyName("banner")]
    public string? Banner { get; set; }
    
    [JsonPropertyName("accent_color")]
    public int? AccentColor { get; set; }
    
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
    
    [JsonPropertyName("verified")]
    public bool? Verified { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("flags")]
    public int? Flags { get; set; }
    
    [JsonPropertyName("premium_type")]
    public int? PremiumType { get; set; }
    
    [JsonPropertyName("public_flags")]
    public int? PublicFlags { get; set; }
    
    [JsonPropertyName("avatar_decoration")]
    public string? AvatarDecoration { get; set; }
}