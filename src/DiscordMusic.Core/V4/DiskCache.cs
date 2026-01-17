using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Key = string;
using ID = string;

namespace DiscordMusic.Core.V4;

internal class DiskCache<T>(
    IFileSystem fileSystem,
    IJsonSerializer jsonSerializer,
    ILogger<DiskCache<T>> logger,
    IOptions<CacheOptions> cacheOptions)
{
    private IDirectoryInfo? _cacheDir;
    private readonly AsyncLock _lock = new();
    private bool _initialized;
    private ByteSize _cacheSize;

    private const string MetadataExtension = ".metadata";
    private const string DataExtension = ".data";

    public async Task<ErrorOr<Success>> InitAsync(CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_initialized)
        {
            return Result.Success;
        }

        var dir = GetCacheDir(logger, cacheOptions, fileSystem);
        if (dir.IsError)
        {
            return dir.Errors;
        }

        _cacheDir = dir.Value;

        var cacheSize = new ByteSize();
        foreach (var metadataFile in fileSystem.Directory.EnumerateFiles(dir.Value.FullName)
                     .Where(file => file.EndsWith(MetadataExtension)))
        {
            var dataFile = Path.Combine(Path.GetFileNameWithoutExtension(metadataFile),
                DataExtension);

            var metadata =
                jsonSerializer.Deserialize<T>(
                    await fileSystem.File.ReadAllTextAsync(metadataFile, ct));

            if (metadata is null)
            {
                logger.LogWarning("Invalid metadata file {MetadataFile}", metadataFile);

                if (fileSystem.File.Exists(dataFile))
                {
                    logger.LogDebug(
                        "Delete data file {DataFile} because of invalid metadata {MetadataFile}",
                        dataFile, metadataFile);
                    fileSystem.File.Delete(dataFile);
                }

                logger.LogDebug("Delete invalid metadata file {MetadataFile}", metadataFile);
                fileSystem.File.Delete(metadataFile);
                continue;
            }

            cacheSize += fileSystem.FileInfo.New(metadataFile).Length.Bytes();
            cacheSize += fileSystem.FileInfo.New(dataFile).Length.Bytes();
        }

        _cacheSize = cacheSize;
        await ShrinkToMaxSizeAsync(ct);
        _initialized = true;
        return Result.Success;
    }

    public async Task<ErrorOr<Success>> SaveOrUpdateMetadataAsync(Key key, T metadata,
        CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_initialized)
        {
            return Error.Unexpected(
                description: "DiskCache not initialized.");
        }

        var id = KeyToId(key);
        var serialized = jsonSerializer.Serialize(metadata);
        var metadataFilePath = Path.Combine(_cacheDir!.FullName, id, MetadataExtension);
        await fileSystem.File.WriteAllTextAsync(metadataFilePath, serialized, ct);
        _cacheSize += fileSystem.FileInfo.New(metadataFilePath).Length.Bytes();

        await ShrinkToMaxSizeAsync(ct);
        return Result.Success;
    }

    public async Task<ErrorOr<T>> RetrieveAsync(Key key, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_initialized)
        {
            return Error.Unexpected(
                description: "DiskCache not initialized.");
        }

        var id = KeyToId(key);
        var metadataFilePath = Path.Combine(_cacheDir!.FullName, id, MetadataExtension);

        if (!fileSystem.File.Exists(metadataFilePath))
        {
            return Error.NotFound(
                description: $"No metadata found for id {id} at path {metadataFilePath}");
        }

        var content = await fileSystem.File.ReadAllTextAsync(metadataFilePath, ct);
        var metadata = jsonSerializer.Deserialize<T>(content);
        if (metadata is null)
        {
            return Error.Unexpected(
                description:
                $"Failed to deserialize metadata for id {id} at path {metadataFilePath}");
        }

        return metadata;
    }

    public async Task<ErrorOr<IFileInfo>> GetMetadataPathAsync(Key key, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_initialized)
        {
            return Error.Unexpected(
                description: "DiskCache not initialized.");
        }

        var id = KeyToId(key);
        return ErrorOrFactory.From(fileSystem.FileInfo.New(Path.Combine(_cacheDir!.FullName, id, MetadataExtension)));
    }

    public async Task<ErrorOr<IFileInfo>> GetDataPathAsync(Key key, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (!_initialized)
        {
            return Error.Unexpected(
                description: "DiskCache not initialized.");
        }

        var id = KeyToId(key);
        return ErrorOrFactory.From(fileSystem.FileInfo.New(Path.Combine(_cacheDir!.FullName, id, DataExtension)));
    }
    
    private static ID KeyToId(Key key)
    {
        var messageBytes = Encoding.UTF8.GetBytes(key);
        var hashValue = SHA256.HashData(messageBytes);
        var hexString = Convert.ToHexString(hashValue);
        return hexString;
    }

    private async Task ShrinkToMaxSizeAsync(CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_cacheSize > ByteSize.Parse(cacheOptions.Value.MaxSize))
        {
            logger.LogWarning(
                "Cache size {CacheSize} exceeds capacity {ByteSize}. Deleting old data.",
                _cacheSize, ByteSize.Parse(cacheOptions.Value.MaxSize));
            var diff = ByteSize.FromBytes(
                Math.Abs((_cacheSize - ByteSize.Parse(cacheOptions.Value.MaxSize)).Bytes));

            var deletedSize = ByteSize.FromBytes(0);
            foreach (var metadataFile in
                     fileSystem.Directory.EnumerateFiles(_cacheDir!.FullName)
                         .Where(file => file.EndsWith(MetadataExtension))
                         .OrderByDescending(file =>
                             fileSystem.FileInfo.New(file).LastAccessTimeUtc))
            {
                var dataFile = Path.Combine(Path.GetFileNameWithoutExtension(metadataFile),
                    DataExtension);

                var entrySize = (fileSystem.FileInfo.New(metadataFile).Length +
                                 fileSystem.FileInfo.New(dataFile).Length).Bytes();
                deletedSize += entrySize;

                if (deletedSize >= diff)
                {
                    continue;
                }

                logger.LogDebug(
                    "Delete metadata {MetaDataFile} and {DataFile} to reduce cache size by {EntrySize}.",
                    metadataFile, dataFile, entrySize);
                fileSystem.File.Delete(dataFile);
                fileSystem.File.Delete(metadataFile);
                _cacheSize -= entrySize;
            }
        }
    }

    private static ErrorOr<IDirectoryInfo> GetCacheDir(ILogger<DiskCache<T>> logger,
        IOptions<CacheOptions> cacheOptions,
        IFileSystem fileSystem)
    {
        var cacheLocation = GetCacheDirBasedOnConfigOrOs(logger, cacheOptions, fileSystem);

        try
        {
            if (cacheLocation.Exists())
            {
                return ErrorOrFactory.From(cacheLocation);
            }

            logger.LogDebug(
                "Cache location {Location} does not exist. Creating directory.",
                cacheLocation
            );
            cacheLocation.Create();
            return ErrorOrFactory.From(cacheLocation);
        }
        catch (Exception e)
        {
            return Error.Unexpected(e.Message);
        }
    }

    private static IDirectoryInfo GetCacheDirBasedOnConfigOrOs(
        ILogger<DiskCache<T>> logger,
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
