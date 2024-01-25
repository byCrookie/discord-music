using System.Text.Json.Serialization;

namespace DiscordMusic.Core.Discord.Gateway;

internal sealed class SessionStartLimitResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }
    
    [JsonPropertyName("reset_after")]
    public int ResetAfter { get; set; }
    
    [JsonPropertyName("max_concurrency")]
    public int MaxConcurrency { get; set; }

    public SessionStartLimitResponse(int total, int remaining, int resetAfter, int maxConcurrency)
    {
        Total = total;
        Remaining = remaining;
        ResetAfter = resetAfter;
        MaxConcurrency = maxConcurrency;
    }
}