using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Cli.Discord.Options;

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

    [UsedImplicitly]
    [ConfigurationKeyName("whitelist")]
    public List<string> Whitelist { get; [UsedImplicitly] init; } = [];

    [UsedImplicitly]
    [ConfigurationKeyName("blacklist")]
    public List<string> Blacklist { get; [UsedImplicitly] init; } = [];

    [ConfigurationKeyName("ffmpeg")]
    public string Ffmpeg { get; [UsedImplicitly] init; } = "ffmpeg";

    [ConfigurationKeyName("ytdlp")]
    public string Ytdlp { get; [UsedImplicitly] init; } = "yt-dlp";
}
