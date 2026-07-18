using System.IO.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Storage;

internal sealed class StorageCacheWatcherService(
    IFileSystem fileSystem,
    IOptions<StorageOptions> storageOptions,
    IStoragePathProvider storagePathProvider,
    IStorageCacheTrimmer cacheTrimmer,
    ILogger<StorageCacheWatcherService> logger
) : BackgroundService
{
    private static readonly TimeSpan TrimDebounce = TimeSpan.FromSeconds(2);
    private int _trimRequested;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!StorageSizeParser.TryParseBytes(storageOptions.Value.MaxSize, out var maxBytes))
        {
            logger.LogError(
                "Invalid storage max size {MaxSize}. Cache watcher is disabled.",
                storageOptions.Value.MaxSize
            );
            return;
        }

        if (maxBytes <= 0)
        {
            logger.LogWarning(
                "Storage max size is {MaxBytes}. Cache watcher is disabled.",
                maxBytes
            );
            return;
        }

        var storagePath = storagePathProvider.StorageDirectory().FullName;
        if (!fileSystem.Directory.Exists(storagePath))
        {
            logger.LogInformation("Creating storage directory {StoragePath}.", storagePath);
            fileSystem.Directory.CreateDirectory(storagePath);
        }

        logger.LogInformation(
            "Storage watcher is running. Path={StoragePath}, MaxSize={MaxSize}, MaxBytes={MaxBytes}, TrimDebounce={TrimDebounce}",
            storagePath,
            storageOptions.Value.MaxSize,
            maxBytes,
            TrimDebounce
        );

        using var watcher = fileSystem.FileSystemWatcher.New(storagePath);
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        watcher.NotifyFilter =
            NotifyFilters.FileName
            | NotifyFilters.Size
            | NotifyFilters.LastWrite
            | NotifyFilters.CreationTime;

        FileSystemEventHandler onChanged = (_, _) => SignalTrim();
        RenamedEventHandler onRenamed = (_, _) => SignalTrim();

        watcher.Created += onChanged;
        watcher.Changed += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;

        try
        {
            await TrimCacheAsync(storagePath, maxBytes, stoppingToken);

            using var timer = new PeriodicTimer(TrimDebounce);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (Interlocked.Exchange(ref _trimRequested, 0) == 0)
                {
                    continue;
                }

                await TrimCacheAsync(storagePath, maxBytes, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Storage cache watcher is stopping.");
        }
        finally
        {
            watcher.Created -= onChanged;
            watcher.Changed -= onChanged;
            watcher.Deleted -= onChanged;
            watcher.Renamed -= onRenamed;
        }
    }

    private void SignalTrim()
    {
        Interlocked.Exchange(ref _trimRequested, 1);
    }

    private Task TrimCacheAsync(
        string storagePath,
        long maxBytes,
        CancellationToken cancellationToken
    )
    {
        return cacheTrimmer.TrimAsync(storagePath, maxBytes, cancellationToken);
    }
}
