using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Abstractions;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Storage;

internal sealed class StorageCacheTrimmer(
    IFileSystem fileSystem,
    ILogger<StorageCacheTrimmer> logger
) : IStorageCacheTrimmer
{
    private readonly SemaphoreSlim _trimLock = new(1, 1);
    private static readonly Counter<long> StorageTrimRuns =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.storage.trim.runs",
            unit: "1",
            description: "Storage cache trim runs."
        );
    private static readonly Histogram<long> StorageCacheSize =
        DiscordMusicObservability.Meter.CreateHistogram<long>(
            "discord.music.storage.cache.size",
            unit: "By",
            description: "Observed storage cache size."
        );
    private static readonly Counter<long> StorageFilesDeleted =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.storage.files.deleted",
            unit: "1",
            description: "Storage cache files deleted."
        );
    private static readonly Counter<long> StorageBytesDeleted =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.storage.bytes.deleted",
            unit: "By",
            description: "Storage cache bytes deleted."
        );

    public async Task TrimAsync(
        string storagePath,
        long maxBytes,
        CancellationToken cancellationToken
    )
    {
        using var activity = DiscordMusicObservability.StartActivity("storage.cache.trim");
        DiscordMusicObservability.SetTag(activity, "storage.cache.max_size", maxBytes);
        DiscordMusicObservability.SetTag(activity, "storage.path.length", storagePath.Length);

        await _trimLock.WaitAsync(cancellationToken);
        try
        {
            var files = GetCacheFiles(storagePath).ToList();
            var totalBytes = files.Sum(file => file.Length);
            DiscordMusicObservability.SetTag(activity, "storage.cache.file_count", files.Count);
            DiscordMusicObservability.SetTag(activity, "storage.cache.size", totalBytes);
            StorageCacheSize.Record(totalBytes);
            if (totalBytes <= maxBytes)
            {
                logger.LogTrace(
                    "Storage cache is within limit. Size={Size} Limit={Limit}",
                    totalBytes,
                    maxBytes
                );
                StorageTrimRuns.Add(1, new KeyValuePair<string, object?>("result", "within_limit"));
                activity?.SetStatus(ActivityStatusCode.Ok, "within_limit");
                return;
            }

            logger.LogInformation(
                "Storage cache exceeds limit. Size={Size} Limit={Limit}. Trimming old files.",
                totalBytes,
                maxBytes
            );

            foreach (var file in files.OrderBy(file => file.LastAccessTimeUtc))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    logger.LogDebug(
                        "Deleting cached file {File} ({Length} bytes).",
                        file.FullName,
                        file.Length
                    );
                    var length = file.Length;
                    file.Delete();
                    totalBytes -= length;
                    StorageFilesDeleted.Add(1);
                    StorageBytesDeleted.Add(length);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not delete cached file {File}.", file.FullName);
                }

                if (totalBytes <= maxBytes)
                {
                    break;
                }
            }

            DeleteEmptyDirectories(storagePath);
            DiscordMusicObservability.SetTag(activity, "storage.cache.trimmed_size", totalBytes);
            StorageTrimRuns.Add(1, new KeyValuePair<string, object?>("result", "trimmed"));
            activity?.SetStatus(ActivityStatusCode.Ok, "trimmed");
        }
        catch (Exception ex)
        {
            StorageTrimRuns.Add(1, new KeyValuePair<string, object?>("result", "failed"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            _trimLock.Release();
        }
    }

    private IEnumerable<IFileInfo> GetCacheFiles(string storagePath)
    {
        if (!fileSystem.Directory.Exists(storagePath))
        {
            return [];
        }

        return fileSystem
            .DirectoryInfo.New(storagePath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => !file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            .Where(file => file.Exists);
    }

    private void DeleteEmptyDirectories(string storagePath)
    {
        foreach (
            var directory in fileSystem
                .DirectoryInfo.New(storagePath)
                .EnumerateDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(directory => directory.FullName.Length)
        )
        {
            try
            {
                if (!directory.EnumerateFileSystemInfos().Any())
                {
                    directory.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogTrace(
                    ex,
                    "Could not delete empty cache directory {Directory}.",
                    directory.FullName
                );
            }
        }
    }
}
