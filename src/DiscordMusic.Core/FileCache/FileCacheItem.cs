using System.Text.Json.Serialization;

namespace DiscordMusic.Core.FileCache;

public readonly record struct FileCacheItem<TKey, TItem>(
    [property: JsonPropertyName("key")] TKey Key,
    [property: JsonPropertyName("item")] TItem Item,
    [property: JsonPropertyName("id")] Guid Id
)
    where TItem : notnull
    where TKey : notnull;
