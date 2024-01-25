using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class InstallParams
{
    [JsonPropertyName("scopes")]
    public string[] Scopes { get; set; } = Array.Empty<string>();
    
    [JsonPropertyName("permissions")]
    public string Permissions { get; set; } = null!;
}