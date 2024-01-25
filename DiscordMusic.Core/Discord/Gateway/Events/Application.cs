using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Application
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;
    
    [JsonPropertyName("rpc_origins")]
    public string[]? RpcOrigins { get; set; } = null!;
    
    [JsonPropertyName("bot_public")]
    public bool? BotPublic { get; set; }
    
    [JsonPropertyName("bot_require_code_grant")]
    public bool? BotRequireCodeGrant { get; set; }
    
    [JsonPropertyName("bot")]
    public User? Bot { get; set; }
    
    [JsonPropertyName("terms_of_service_url")]
    public string? TermsOfServiceUrl { get; set; }
    
    [JsonPropertyName("privacy_policy_url")]
    public string? PrivacyPolicyUrl { get; set; }
    
    [JsonPropertyName("owner")]
    public User? Owner { get; set; }
    
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    [JsonPropertyName("verify_key")]
    public string? VerifyKey { get; set; }
    
    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }
    
    [JsonPropertyName("guild")]
    public Guild? Guild { get; set; }
    
    [JsonPropertyName("primary_sku_id")]
    public string? PrimarySkuId { get; set; }
    
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
    
    [JsonPropertyName("cover_image")]
    public string? CoverImage { get; set; }
    
    [JsonPropertyName("flags")]
    public int? Flags { get; set; }
    
    [JsonPropertyName("approximate_guild_count")]
    public int? ApproximateGuildCount { get; set; }
    
    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; set; }
    
    [JsonPropertyName("interactions_endpoint_url")]
    public string? InteractionsEndpointUrl { get; set; }
    
    [JsonPropertyName("role_connections_verification_url")]
    public string? RoleConnectionsVerificationUrl { get; set; }
    
    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }
    
    [JsonPropertyName("install_params")]
    public InstallParams? InstallParams { get; set; }
    
    [JsonPropertyName("custom_install_url")]
    public string? CustomInstallUrl { get; set; }
}