using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Config;

internal class AppPaths(
    ILogger<AppPaths> logger,
    IOptions<CacheOptions> cacheOptions,
    IFileSystem fileSystem
)
{
    public IDirectoryInfo Cache()
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

    public static string Config(ILogger logger)
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            logger.LogDebug(
                "Using XDG_CONFIG_HOME '{XDG_CONFIG_HOME}' as config location",
                xdgConfigHome
            );
            var configDir = Path.Combine(xdgConfigHome, "bycrookie", "discord-music");
            logger.LogDebug("Final XDG config location {Location}", configDir);
            return configDir;
        }

        if (OperatingSystem.IsWindows())
        {
            logger.LogDebug("Using home as config location");
            var windows = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "bycrookie",
                "discord-music"
            );
            logger.LogDebug("Final windows location {Location}", windows);
            return windows;
        }

        logger.LogDebug("Using unix home as config location");
        var unix = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "bycrookie",
            "discord-music"
        );
        logger.LogDebug("Final unix location {Location}", unix);
        return unix;
    }
}
