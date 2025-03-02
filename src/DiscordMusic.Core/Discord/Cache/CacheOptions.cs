﻿using Microsoft.Extensions.Configuration;

namespace DiscordMusic.Core.Discord.Cache;

internal class CacheOptions
{
    public const string SectionName = "cache";

    [ConfigurationKeyName("maxSize")]
    public string MaxSize { get; init; } = "5GB";

    [ConfigurationKeyName("location")]
    public string Location { get; init; } = null!;
}
