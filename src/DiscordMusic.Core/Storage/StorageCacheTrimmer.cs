using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Storage;

internal sealed class StorageCacheTrimmer(
    IFileSystem fileSystem,
    ILogger<StorageCacheTrimmer> logger
) : IStorageCacheTrimmer
{
    private readonly SemaphoreSlim _trimLock = new(1, 1);

    public async Task TrimAsync(
        string storagePath,
        long maxBytes,
        CancellationToken cancellationToken
    )
    {
        await _trimLock.WaitAsync(cancellationToken);
        try
        {
            var files = GetCacheFiles(storagePath).ToList();
            var totalBytes = files.Sum(file => file.Length);
            if (totalBytes <= maxBytes)
            {
                logger.LogTrace(
                    "Storage cache is within limit. Size={Size} Limit={Limit}",
                    totalBytes,
                    maxBytes
                );
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
