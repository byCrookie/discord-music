using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class Activity(string name)
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = name;

    [JsonPropertyName("type")]
    public AcitivityType Type { get; set; }

    [JsonPropertyName("state")]
    public string? Status { get; set; }
    
    [JsonPropertyName("afk")]
    public bool Afk { get; set; }
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}