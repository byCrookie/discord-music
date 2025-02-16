using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Humanizer;
using Humanizer.Bytes;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.FileCache;

public class FileCache<TKey, TItem>(
    IFileSystem fileSystem,
    IJsonSerializer jsonSerializer,
    ILogger<FileCache<TKey, TItem>> logger
) : IFileCache<TKey, TItem>
    where TKey : notnull
    where TItem : notnull
{
    private const string DataFile = "_data.json";
    private const string ItemFile = "_item";
    private readonly Dictionary<TKey, FileCacheItem<TKey, TItem>> _cache = new();
    private readonly AsyncLock _lock = new();
    private bool _indexed;
    private IDirectoryInfo? _location;

    public async Task<ErrorOr<CacheItem<TKey, TItem>>> AddOrUpdateAsync(TKey key, TKey updateKey, TItem item,
        CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_indexed)
        {
            return Error.Unexpected(description: "Cache must be indexed");
        }

        if (!_cache.Remove(key, out var fileCacheItem))
        {
            return await AddAsync(updateKey, item, ct);
        }

        var dataFile = GetDataFile(fileCacheItem.Id);
        var cacheItem = new FileCacheItem<TKey, TItem>(updateKey, item, fileCacheItem.Id);

        _cache[updateKey] = cacheItem;

        var json = jsonSerializer.Serialize(cacheItem);
        await fileSystem.File.WriteAllTextAsync(dataFile.FullName, json, ct);

        var itemFile = GetItemFile(fileCacheItem.Id);
        return new CacheItem<TKey, TItem>(updateKey, item, itemFile);
    }

    public async Task<ErrorOr<ByteSize>> GetSizeAsync(CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_indexed)
        {
            return Error.Unexpected(description: "Cache must be indexed");
        }

        var size = _location!
            .EnumerateFiles()
            .Where(file => file.Name.EndsWith(DataFile) || file.Name.EndsWith(ItemFile))
            .Sum(file => file.Length);

        return size.Bytes();
    }

    public async Task<ErrorOr<Success>> ClearAsync(CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_indexed)
        {
            return Error.Unexpected(description: "Cache must be indexed");
        }

        var files = fileSystem
            .Directory.GetFiles(_location!.FullName)
            .Where(file => file.EndsWith(DataFile) || file.EndsWith(ItemFile));

        _cache.Clear();

        foreach (var file in files)
        {
            fileSystem.File.Delete(file);
        }

        return new Success();
    }

    public async Task<ErrorOr<Success>> IndexAsync(IDirectoryInfo location, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_indexed)
        {
            return Result.Success;
        }

        if (!location.Exists())
        {
            return Error.Validation(description: $"Directory for cache {location.FullName} does not exist");
        }

        foreach (var dataFile in location.EnumerateFiles().Where(file => file.Name.EndsWith(DataFile)))
        {
            var content = await fileSystem.File.ReadAllTextAsync(dataFile.FullName, ct);
            var data = jsonSerializer.Deserialize<FileCacheItem<TKey, TItem>>(content);

            if (!_cache.TryAdd(data.Key, data))
            {
                logger.LogWarning("Key {Key} already exists in cache, item will be ignored", data.Key);
            }
        }

        _location = location;
        _indexed = true;
        return new Success();
    }

    public async Task<ErrorOr<CacheItem<TKey, TItem>>> GetOrAddAsync(TKey key, TItem item, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_indexed)
        {
            return Error.Unexpected(description: "Cache must be indexed");
        }

        if (_cache.TryGetValue(key, out var fileCacheItem))
        {
            if (GetDataFile(fileCacheItem.Id).Exists())
            {
                return new CacheItem<TKey, TItem>(fileCacheItem.Key, fileCacheItem.Item, GetItemFile(fileCacheItem.Id));
            }

            _cache.Remove(key);
        }

        return await AddAsync(key, item, ct);
    }

    private async Task<ErrorOr<CacheItem<TKey, TItem>>> AddAsync(TKey key, TItem item, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var dataFile = GetDataFile(id);
        var itemFile = GetItemFile(id);
        var cacheItem = new FileCacheItem<TKey, TItem>(key, item, id);

        _cache[key] = cacheItem;

        var json = jsonSerializer.Serialize(cacheItem);
        await fileSystem.File.WriteAllTextAsync(dataFile.FullName, json, ct);

        return new CacheItem<TKey, TItem>(key, item, itemFile);
    }

    private IFileInfo GetItemFile(Guid id)
    {
        return fileSystem.FileInfo.New(Path.Combine(_location!.FullName, $"{id:N}{ItemFile}"));
    }

    private IFileInfo GetDataFile(Guid id)
    {
        return fileSystem.FileInfo.New(Path.Combine(_location!.FullName, $"{id:N}{DataFile}"));
    }
}
