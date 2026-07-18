using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Storage;

internal sealed class StoragePathProvider(
    ILogger<StoragePathProvider> logger,
    IOptions<StorageOptions> storageOptions,
    IFileSystem fileSystem,
    IEnvironmentVariables environmentVariables
) : IStoragePathProvider
{
    private readonly Lock _lock = new();
    private IDirectoryInfo? _storageDirectory;

    public IDirectoryInfo StorageDirectory()
    {
        lock (_lock)
        {
            return _storageDirectory ??= ResolveStorageDirectory();
        }
    }

    private IDirectoryInfo ResolveStorageDirectory()
    {
        if (!string.IsNullOrWhiteSpace(storageOptions.Value.Path))
        {
            var configured = fileSystem.DirectoryInfo.New(storageOptions.Value.Path);
            logger.LogInformation(
                "Using configured storage path {StoragePath}.",
                configured.FullName
            );
            return configured;
        }

        var xdgCacheHome = environmentVariables.GetVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgCacheHome))
        {
            var xdg = fileSystem.DirectoryInfo.New(
                fileSystem.Path.Combine(xdgCacheHome, "bycrookie", "discord-music")
            );
            logger.LogInformation(
                "Using XDG storage path {StoragePath}. XDG_CACHE_HOME={XdgCacheHome}",
                xdg.FullName,
                xdgCacheHome
            );
            return xdg;
        }

        if (OperatingSystem.IsWindows())
        {
            var windows = fileSystem.DirectoryInfo.New(
                fileSystem.Path.Combine(
                    environmentVariables.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "bycrookie",
                    "discord-music",
                    "storage"
                )
            );
            logger.LogInformation("Using Windows storage path {StoragePath}.", windows.FullName);
            return windows;
        }

        var unix = fileSystem.DirectoryInfo.New(
            fileSystem.Path.Combine(
                environmentVariables.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "bycrookie",
                "discord-music"
            )
        );
        logger.LogInformation("Using Unix storage path {StoragePath}.", unix.FullName);
        return unix;
    }
}
