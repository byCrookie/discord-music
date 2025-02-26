using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Audio;

public class AudioOptions
{
    public const string SectionName = "audio";

    [Required]
    [ConfigurationKeyName("buffer")]
    public string Buffer { get; init; } = "00:00:00.300";

    [JsonIgnore]
    public TimeSpan BufferTime => TimeSpan.Parse(Buffer);
}
