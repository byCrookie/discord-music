using System.IO.Abstractions;

namespace DiscordMusic.Core.FileCache;

public readonly record struct CacheItem<TKey, TItem>(TKey Key, TItem Item, IFileInfo File)
    where TItem : notnull where TKey : notnull;