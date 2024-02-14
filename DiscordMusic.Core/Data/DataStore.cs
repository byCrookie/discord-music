using System.IO.Abstractions;
using DiscordMusic.Core.Environment;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Data;

internal sealed class DataStore(
    IFileSystem fileSystem,
    IEnvironment environment,
    ILogger<DataStore> logger)
    : IDataStore
{
    private const string AppDirectory = "discord-music";

    public IDirectoryInfo Require(string directory)
    {
        if (!TryGetAppData(out var appdata))
        {
            throw new Exception("Cannot retrieve appdata path.");
        }

        var appDataPath = fileSystem.Path.Combine(appdata!.FullName, directory);
        logger.LogDebug("Path for {Path} is {AppDataPath}", directory, appDataPath);
        var trackDirectory = fileSystem.DirectoryInfo.New(appDataPath);

        if (fileSystem.Directory.Exists(trackDirectory.FullName))
        {
            return trackDirectory;
        }

        logger.LogDebug("Creating path {Path}", trackDirectory);
        fileSystem.Directory.CreateDirectory(appDataPath);
        return trackDirectory;
    }

    private bool TryGetAppData(out IDirectoryInfo? appData)
    {
        var appDataPath = environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData).FullName;
        logger.LogTrace("AppData path is {AppDataPath}", appDataPath);

        if (fileSystem.Directory.Exists(appDataPath))
        {
            var appPath = fileSystem.Path.Combine(appDataPath, AppDirectory);
            logger.LogTrace("Appdata path is {AppPath}", appPath);

            if (fileSystem.Directory.Exists(appPath))
            {
                logger.LogTrace("Path {Path} exists", appPath);
                appData = fileSystem.DirectoryInfo.New(appPath);
                return true;
            }

            try
            {
                logger.LogTrace("Creating path {Path}", appPath);
                fileSystem.Directory.CreateDirectory(appPath);
                appData = fileSystem.DirectoryInfo.New(appPath);
                return true;
            }
            catch (Exception)
            {
                logger.LogWarning("Unable to create path {Path}", appPath);
                appData = null;
                return false;
            }
        }

        logger.LogWarning("AppData path {Path} does not exist", appDataPath);
        appData = null;
        return false;
    }
}
