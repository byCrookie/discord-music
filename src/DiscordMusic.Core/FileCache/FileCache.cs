using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.FileCache;

public class FileCache<TKey, TItem>(
    IFileSystem fileSystem,
    IJsonSerializer jsonSerializer,
    ILogger<FileCache<TKey, TItem>> logger,
    ByteSize capacity
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

    public async Task<ErrorOr<CacheItem<TKey, TItem>>> AddOrUpdateAsync(
        TKey key,
        TKey updateKey,
        TItem item,
        ByteSize approxSize,
        CancellationToken ct
    )
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_indexed)
        {
            return Error.Unexpected(description: "Cache must be indexed");
        }

        if (!_cache.Remove(key, out var fileCacheItem))
        {
            return await AddAsync(updateKey, item, approxSize, ct);
        }

        var dataFile = GetDataFile(fileCacheItem.Id);
        var cacheItem = new FileCacheItem<TKey, TItem>(updateKey, item, fileCacheItem.Id);

        _cache[updateKey] = cacheItem;

        var json = jsonSerializer.Serialize(cacheItem);
        await fileSystem.File.WriteAllTextAsync(dataFile.FullName, json, ct);

        var itemFile = GetItemFile(fileCacheItem.Id);

        if (dataFile.Length.Bytes() + itemFile.Length.Bytes() > approxSize)
        {
            return new CacheItem<TKey, TItem>(updateKey, item, itemFile);
        }

        var clear = await ClearOldestToMakeSpaceAsync(approxSize, ct);

        if (clear.IsError)
        {
            return clear.Errors;
        }

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

        return Result.Success;
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
            return Error.Validation(
                description: $"Directory for cache {location.FullName} does not exist"
            );
        }

        foreach (
            var dataFile in location.EnumerateFiles().Where(file => file.Name.EndsWith(DataFile))
        )
        {
            var content = await fileSystem.File.ReadAllTextAsync(dataFile.FullName, ct);
            var data = jsonSerializer.Deserialize<FileCacheItem<TKey, TItem>>(content);

            if (!_cache.TryAdd(data.Key, data))
            {
                logger.LogWarning(
                    "Key {Key} already exists in cache, item will be ignored",
                    data.Key
                );
            }
        }

        _location = location;
        _indexed = true;
        return Result.Success;
    }

    public async Task<ErrorOr<CacheItem<TKey, TItem>>> GetOrAddAsync(
        TKey key,
        TItem item,
        ByteSize approxSize,
        CancellationToken ct
    )
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
                return new CacheItem<TKey, TItem>(
                    fileCacheItem.Key,
                    fileCacheItem.Item,
                    GetItemFile(fileCacheItem.Id)
                );
            }

            _cache.Remove(key);
        }

        return await AddAsync(key, item, approxSize, ct);
    }

    private async Task<ErrorOr<Success>> ClearOldestToMakeSpaceAsync(
        ByteSize approxSize,
        CancellationToken ct
    )
    {
        var size = _location!
            .EnumerateFiles()
            .Where(file => file.Name.EndsWith(DataFile) || file.Name.EndsWith(ItemFile))
            .Sum(file => file.Length)
            .Bytes();

        if (size + approxSize <= capacity)
        {
            logger.LogDebug(
                "Cache size {Size} is less than capacity {Capacity}, no need to clear",
                size,
                capacity
            );
            return Result.Success;
        }

        logger.LogDebug(
            "{Size} + {ApproxSize} > {Capacity}, clearing oldest files",
            size,
            approxSize,
            capacity
        );

        var files = _location!
            .EnumerateFiles()
            .Where(file => file.Name.EndsWith(DataFile) || file.Name.EndsWith(ItemFile))
            .OrderBy(file => file.CreationTimeUtc)
            .Select(file => new
            {
                File = file,
                file.Length,
                Bytes = file.Length.Bytes(),
            })
            .ToList();

        var maxToFree = files.Sum(file => file.Length).Bytes();

        if (maxToFree <= approxSize)
        {
            return Error.Unexpected(
                description: $"Cannot free enough space for {approxSize}. Not enough files to delete. Only {maxToFree} can be freed."
            );
        }

        var freed = 0.Bytes();
        foreach (var file in files)
        {
            try
            {
                if (file.File.Name.EndsWith(DataFile))
                {
                    logger.LogTrace(
                        "Deleting data file {File} to free {FileSize}",
                        file.File.FullName,
                        file.Bytes
                    );
                    var content = await fileSystem.File.ReadAllTextAsync(file.File.FullName, ct);
                    var data = jsonSerializer.Deserialize<FileCacheItem<TKey, TItem>>(content);
                    _cache.Remove(data.Key);
                    fileSystem.File.Delete(file.File.FullName);
                    freed += file.Bytes;
                }

                if (file.File.Name.EndsWith(ItemFile))
                {
                    logger.LogTrace(
                        "Deleting item file {File} to free {FileSize}",
                        file.File.FullName,
                        file.Bytes
                    );
                    fileSystem.File.Delete(file.File.FullName);
                    freed += file.Bytes;
                }

                if (size - freed + approxSize <= capacity)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(
                    e,
                    "Failed to delete file {File} to free {FileSize}",
                    file.File.FullName,
                    file.Bytes
                );
            }
        }

        if (size - freed + approxSize > capacity)
        {
            return Error.Unexpected(
                description: $"Only freed {freed} bytes, still not enough space for {approxSize} below capacity {capacity}"
            );
        }

        logger.LogDebug(
            "Cleared {Freed} bytes to make space for {ApproxSize} below capacity {Capacity}",
            freed,
            approxSize,
            capacity
        );
        return Result.Success;
    }

    private async Task<ErrorOr<CacheItem<TKey, TItem>>> AddAsync(
        TKey key,
        TItem item,
        ByteSize approxSize,
        CancellationToken ct
    )
    {
        var clear = await ClearOldestToMakeSpaceAsync(approxSize, ct);

        if (clear.IsError)
        {
            return clear.Errors;
        }

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
