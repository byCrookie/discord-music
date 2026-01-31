using System.IO.Abstractions;
using DiscordMusic.Core.Config;
using DiscordMusic.Core.FileCache;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Discord.Cache;

internal class MusicCache(
    IOptions<CacheOptions> cacheOptions,
    IFileSystem fileSystem,
    IJsonSerializer jsonSerializer,
    ILogger<MusicCache> logger,
    ILogger<FileCache<string, Track>> fileCacheLogger,
    AppPaths appPaths
) : IMusicCache
{
    private readonly FileCache<string, Track> _fileCache = new(
        fileSystem,
        jsonSerializer,
        fileCacheLogger,
        ByteSize.Parse(cacheOptions.Value.MaxSize)
    );

    public async Task<ErrorOr<Success>> ClearAsync(CancellationToken ct)
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index;
        }

        return await _fileCache.ClearAsync(ct);
    }

    public async Task<ErrorOr<IFileInfo>> GetOrAddTrackAsync(
        Track track,
        ByteSize approxSize,
        CancellationToken ct
    )
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index.Errors;
        }

        var cache = await _fileCache.GetOrAddAsync(track.Url, track, approxSize, ct);
        return cache.IsError ? cache.Errors : cache.Value.File.ToErrorOr();
    }

    public async Task<ErrorOr<IFileInfo>> AddOrUpdateTrackAsync(
        Track track,
        Track updatedTrack,
        ByteSize approxSize,
        CancellationToken ct
    )
    {
        var index = await IndexAsync(ct);

        if (index.IsError)
        {
            return index.Errors;
        }

        var cache = await _fileCache.AddOrUpdateAsync(
            track.Url,
            updatedTrack.Url,
            updatedTrack,
            approxSize,
            ct
        );
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
        var cacheLocation = appPaths.Cache();

        try
        {
            if (!cacheLocation.Exists())
            {
                logger.LogDebug(
                    "Cache location missing; creating directory. Path={CachePath}",
                    cacheLocation.FullName
                );
                cacheLocation.Create();
            }
        }
        catch (Exception e)
        {
            logger.LogError(
                e,
                "Failed to initialize cache location. Path={CachePath}",
                cacheLocation.FullName
            );
            return Error
                .Unexpected(
                    code: "Cache.InitFailed",
                    description: "I couldn't access the cache directory."
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "cache.init")
                .WithMetadata("cachePath", cacheLocation.FullName)
                .WithException(e);
        }

        return await _fileCache.IndexAsync(cacheLocation, ct);
    }
}
