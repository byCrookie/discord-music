using System.IO.Abstractions;
using DiscordMusic.Core.FileCache;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Humanizer.Bytes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Discord.Cache;

internal class MusicCache(
    IOptions<CacheOptions> cacheOptions,
    IFileSystem fileSystem,
    IJsonSerializer jsonSerializer,
    ILogger<MusicCache> logger,
    ILogger<FileCache<string, Track>> fileCacheLogger
) : IMusicCache
{
    private readonly FileCache<string, Track> _fileCache = new(fileSystem, jsonSerializer, fileCacheLogger);

    public async Task<ErrorOr<Success>> ClearAsync(CancellationToken ct)
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index;
        }

        return await _fileCache.ClearAsync(ct);
    }

    public async Task<ErrorOr<IFileInfo>> GetOrAddTrackAsync(Track track, CancellationToken ct)
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index.Errors;
        }

        var cache = await _fileCache.GetOrAddAsync(track.Url, track, ct);
        return cache.IsError ? cache.Errors : cache.Value.File.ToErrorOr();
    }

    public async Task<ErrorOr<IFileInfo>> AddOrUpdateTrackAsync(Track track, Track updatedTrack, CancellationToken ct)
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index.Errors;
        }

        var cache = await _fileCache.AddOrUpdateAsync(track.Url, updatedTrack.Url, updatedTrack, ct);
        return cache.IsError ? cache.Errors : cache.Value.File.ToErrorOr();
    }

    public async Task<ErrorOr<ByteSize>> GetSizeAsync(CancellationToken ct)
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index.Errors;
        }

        var size = await _fileCache.GetSizeAsync(ct);

        if (size.IsError)
        {
            return size.Errors;
        }

        return size.Value;
    }

    private async Task<ErrorOr<Success>> IndexAsync(CancellationToken ct)
    {
        var cacheLocation = GetCacheLocation(logger, cacheOptions, fileSystem);
        return await _fileCache.IndexAsync(cacheLocation, ct);
    }

    private static IDirectoryInfo GetCacheLocation(
        ILogger<MusicCache> logger,
        IOptions<CacheOptions> cacheOptions,
        IFileSystem fileSystem
    )
    {
        if (string.IsNullOrWhiteSpace(cacheOptions.Value.Location))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var cacheDir = fileSystem.DirectoryInfo.New(Path.Combine(appData, "discord-music", "cache"));
            logger.LogWarning("Cache location not set, using default {Location}", cacheDir);

            if (cacheDir.Exists())
            {
                return cacheDir;
            }

            logger.LogDebug("Creating cache directory {Location}", cacheDir);
            cacheDir.Create();

            return cacheDir;
        }

        var cacheLocation = fileSystem.DirectoryInfo.New(cacheOptions.Value.Location);

        if (cacheLocation.Exists())
        {
            return cacheLocation;
        }

        logger.LogDebug("Creating cache directory {Location}", cacheLocation);
        cacheLocation.Create();

        return cacheLocation;
    }
}
