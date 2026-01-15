using System.IO.Abstractions;
using ErrorOr;
using Humanizer;

namespace DiscordMusic.Core.FileCache;

public interface IFileCache<TKey, TItem>
    where TItem : notnull
    where TKey : notnull
{
    public Task<ErrorOr<Success>> IndexAsync(IDirectoryInfo location, CancellationToken ct);

    public Task<ErrorOr<CacheItem<TKey, TItem>>> GetOrAddAsync(
        TKey key,
        TItem item,
        ByteSize approxSize,
        CancellationToken ct
    );

    public Task<ErrorOr<CacheItem<TKey, TItem>>> AddOrUpdateAsync(
        TKey key,
        TKey updateKey,
        TItem item,
        ByteSize approxSize,
        CancellationToken ct
    );

    public Task<ErrorOr<ByteSize>> GetSizeAsync(CancellationToken ct);
    public Task<ErrorOr<Success>> ClearAsync(CancellationToken ct);
}
