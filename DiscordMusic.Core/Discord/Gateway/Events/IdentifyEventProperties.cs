using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class IdentifyEventProperties(string os, string browser, string device)
{
    [JsonPropertyName("os")]
    public string Os { get; set; } = os;

    [JsonPropertyName("browser")]
    public string Browser { get; set; } = browser;

    [JsonPropertyName("device")]
    public string Device { get; set; } = device;
}