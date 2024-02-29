using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Options;

public class DiscordOptions
{
    public const string SectionName = "discord";

    [Required]
    [ConfigurationKeyName("applicationId")]
    public string ApplicationId { get; [UsedImplicitly] init; } = null!;

    [Required]
    [ConfigurationKeyName("token")]
    public string Token { get; [UsedImplicitly] init; } = null!;

    [ConfigurationKeyName("prefix")]
    public string Prefix { get; [UsedImplicitly] init; } = "!";

    [Required]
    [ConfigurationKeyName("guildId")]
    public ulong GuildId { get; [UsedImplicitly] init; }

    [Required]
    [ConfigurationKeyName("channelId")]
    public ulong ChannelId { get; [UsedImplicitly] init; }

    [UsedImplicitly]
    [ConfigurationKeyName("whitelist")]
    public List<string> Whitelist { get; [UsedImplicitly] init; } = [];

    [UsedImplicitly]
    [ConfigurationKeyName("blacklist")]
    public List<string> Blacklist { get; [UsedImplicitly] init; } = [];

    [Required]
    [ConfigurationKeyName("message")]
    public string Message { get; [UsedImplicitly] init; } = null!;
}
