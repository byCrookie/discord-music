using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Storage;

internal sealed class StorageCacheWatcherService(
    IFileSystem fileSystem,
    IOptions<StorageOptions> storageOptions,
    IStoragePathProvider storagePathProvider,
    IStorageCacheTrimmer cacheTrimmer,
    ILogger<StorageCacheWatcherService> logger,
    TimeProvider timeProvider
) : BackgroundService
{
    private static readonly TimeSpan TrimDebounce = TimeSpan.FromSeconds(2);
    private int _trimRequested;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = DiscordMusicObservability.StartActivity("storage.cache.watch");
        DiscordMusicObservability.SetTag(
            activity,
            "storage.max_size",
            storageOptions.Value.MaxSize
        );

        if (!StorageSizeParser.TryParseBytes(storageOptions.Value.MaxSize, out var maxBytes))
        {
            RecordWatcherEvent("disabled", "invalid_max_size");
            activity?.SetStatus(ActivityStatusCode.Error, "invalid_max_size");
            logger.LogError(
                "Invalid storage max size {MaxSize}. Cache watcher is disabled.",
                storageOptions.Value.MaxSize
            );
            return;
        }

        if (maxBytes <= 0)
        {
            RecordWatcherEvent("disabled", "non_positive_max_size");
            activity?.SetStatus(ActivityStatusCode.Ok, "disabled");
            logger.LogWarning(
                "Storage max size is {MaxBytes}. Cache watcher is disabled.",
                maxBytes
            );
            return;
        }

        var storagePath = storagePathProvider.StorageDirectory().FullName;
        DiscordMusicObservability.SetTag(activity, "storage.path.length", storagePath.Length);
        DiscordMusicObservability.SetTag(activity, "storage.cache.max_size", maxBytes);
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

        FileSystemEventHandler onChanged = (_, args) => SignalTrim(args.ChangeType.ToString());
        RenamedEventHandler onRenamed = (_, _) => SignalTrim("Renamed");

        watcher.Created += onChanged;
        watcher.Changed += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += onRenamed;

        try
        {
            RecordWatcherEvent("started", "running");
            await TrimCacheAsync(storagePath, maxBytes, stoppingToken);

            using var timer = new PeriodicTimer(TrimDebounce, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (Interlocked.Exchange(ref _trimRequested, 0) == 0)
                {
                    continue;
                }

                RecordWatcherEvent("trim", "debounced");
                await TrimCacheAsync(storagePath, maxBytes, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            RecordWatcherEvent("stopped", "cancelled");
            activity?.SetStatus(ActivityStatusCode.Ok, "stopped");
            logger.LogInformation("Storage cache watcher is stopping.");
        }
        catch (Exception ex)
        {
            RecordWatcherEvent("failed", "exception");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            watcher.Created -= onChanged;
            watcher.Changed -= onChanged;
            watcher.Deleted -= onChanged;
            watcher.Renamed -= onRenamed;
        }
    }

    private static void RecordWatcherEvent(string eventName, string result)
    {
        DiscordMusicObservability.StorageWatcherEvents.Add(
            1,
            new KeyValuePair<string, object?>("event", eventName),
            new KeyValuePair<string, object?>("result", result)
        );
    }

    private void SignalTrim(string eventName)
    {
        RecordWatcherEvent(eventName, "requested");
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
