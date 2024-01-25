using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway.Events;

public class GatewayEvent<T> : GatewayEvent
{
    [JsonPropertyName("d")]
    public T? Data { get; set; }
}

public class GatewayEvent
{
    [JsonPropertyName("op")]
    public OpCodes OpCode { get; set; }
    
    [JsonPropertyName("s")]
    public int? SequenceNumber { get; set; }
    
    [JsonPropertyName("t")]
    public string? EventName { get; set; }
}