using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace DiscordMusic.Cs.Cli.Discord.Options;

public class CsOptions
{
    public const string SectionName = "cs";

    [UsedImplicitly]
    [ConfigurationKeyName("whitelist")]
    public List<string> Whitelist { get; [UsedImplicitly] init; } = [];

    [Required]
    [ConfigurationKeyName("cfg")]
    public string Cfg { get; [UsedImplicitly] init; } = null!;

    [Required]
    [ConfigurationKeyName("playOnFreeze")]
    public bool PlayOnFreeze { get; [UsedImplicitly] init; }

    [Required]
    [ConfigurationKeyName("listen")]
    public bool Listen { get; [UsedImplicitly] init; }

    [Required]
    [ConfigurationKeyName("isPaused")]
    public bool IsPaused { get; [UsedImplicitly] init; }
}
