using System.ComponentModel.DataAnnotations;

namespace DiscordMusic.Core.Discord.Options;

public class DiscordSecrets
{
    [Required] 
    public string ApplicationId { get; init; } = null!;
    
    [Required] 
    public string Token { get; init; } = null!;
};