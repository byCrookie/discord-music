using System.IO.Abstractions;
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
    ILogger<FileCache<string, Track>> fileCacheLogger
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
        var cacheLocation = GetCacheLocation(logger, cacheOptions, fileSystem);

        try
        {
            if (!cacheLocation.Exists())
            {
                logger.LogDebug(
                    "Cache location {Location} does not exist. Creating directory.",
                    cacheLocation
                );
                cacheLocation.Create();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize cache location");
            return Error.Unexpected(description: "I couldn't access the cache directory.");
        }

        return await _fileCache.IndexAsync(cacheLocation, ct);
    }

    private static IDirectoryInfo GetCacheLocation(
        ILogger<MusicCache> logger,
        IOptions<CacheOptions> cacheOptions,
        IFileSystem fileSystem
    )
    {
        if (!string.IsNullOrWhiteSpace(cacheOptions.Value.Location))
        {
            logger.LogDebug(
                "Using {Location} from environment variable or config file as cache location",
                cacheOptions.Value.Location
            );
            var cacheLocation = fileSystem.DirectoryInfo.New(cacheOptions.Value.Location);
            logger.LogDebug("Final env or config location {Location}", cacheLocation);
            return cacheLocation;
        }

        var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgCacheHome))
        {
            logger.LogDebug(
                "Using XDG_CACHE_HOME '{XDG_CACHE_HOME}' as cache location",
                xdgCacheHome
            );
            var cacheDir = fileSystem.DirectoryInfo.New(
                Path.Combine(xdgCacheHome, "bycrookie", "discord-music")
            );
            logger.LogDebug("Final XDG cache location {Location}", cacheDir);
            return cacheDir;
        }

        if (OperatingSystem.IsWindows())
        {
            logger.LogDebug("Using windows local app data location as cache location");
            var windows = fileSystem.DirectoryInfo.New(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "bycrookie",
                    "discord-music",
                    "cache"
                )
            );
            logger.LogDebug("Final windows location {Location}", windows);
            return windows;
        }

        logger.LogDebug("Using unix home as cache location");
        var unix = fileSystem.DirectoryInfo.New(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "bycrookie",
                "discord-music"
            )
        );
        logger.LogDebug("Final unix location {Location}", unix);
        return unix;
    }
}
