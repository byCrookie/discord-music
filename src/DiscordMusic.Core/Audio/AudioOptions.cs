using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Audio;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class AudioOptions
{
    public const string SectionName = "audio";

    [Required]
    [ConfigurationKeyName("buffer")]
    public required string Buffer { get; init; } = "00:00:00.300";

    [JsonIgnore]
    public TimeSpan BufferTime => TimeSpan.Parse(Buffer);
}
