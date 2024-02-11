using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Cs.Cli.Discord.Options;

public class DiscordCsOptions
{
    public const string SectionName = "discord";

    [Required]
    [ConfigurationKeyName("token")]
    public string Token { get; [UsedImplicitly] init; } = null!;
    
    [Required]
    [ConfigurationKeyName("guildId")]
    public ulong GuildId { get; [UsedImplicitly] init; }
    
    [Required]
    [ConfigurationKeyName("channelId")]
    public ulong ChannelId { get; [UsedImplicitly] init; }
    
    [Required]
    [ConfigurationKeyName("message")]
    public string Message { get; [UsedImplicitly] init; } = null!;

    [Required]
    [ConfigurationKeyName("cscfg")]
    public string CsCfg { get; [UsedImplicitly] init; } = null!;
}