using System.IO.Abstractions;
using DiscordMusic.Cli.Environment;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Data;

internal sealed class DataStore(
    IFileSystem fileSystem,
    IEnvironment environment,
    ILogger<DataStore> logger)
    : IDataStore
{
    private const string AppDirectory = "discord-music";

    public IDirectoryInfo Require(string directory)
    {
        var appDataPath = fileSystem.Path.Combine(GetAppDataPath().FullName, directory);
        logger.LogDebug("Path for {Path} is {AppDataPath}", directory, appDataPath);
        var trackDirectory = fileSystem.DirectoryInfo.New(appDataPath);

        if (fileSystem.Directory.Exists(trackDirectory.FullName))
        {
            return trackDirectory;
        }

        logger.LogDebug("Creating directory {Path}", trackDirectory);
        fileSystem.Directory.CreateDirectory(appDataPath);
        return trackDirectory;
    }

    private IDirectoryInfo GetAppDataPath()
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
                return fileSystem.DirectoryInfo.New(appPath);
            }

            try
            {
                logger.LogTrace("Creating path {Path}", appPath);
                fileSystem.Directory.CreateDirectory(appPath);
                return fileSystem.DirectoryInfo.New(appPath);
            }
            catch (Exception)
            {
                logger.LogWarning("Unable to create path {Path}", appPath);
            }
        }

        var currentDirectory = fileSystem.Directory.GetCurrentDirectory();
        logger.LogWarning("AppData path {Path} does not exist, using current directory {CurrentDir}", appDataPath,
            currentDirectory);
        return fileSystem.DirectoryInfo.New(currentDirectory);
    }
}
