using System.IO.Abstractions;
using DiscordMusic.Cli.Discord.Options;
using DiscordMusic.Cli.Environment;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cli.Data;

internal sealed class DataStore(
    IFileSystem fileSystem,
    IEnvironment environment,
    IOptions<DiscordOptions> discordOptions,
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
        if (!string.IsNullOrWhiteSpace(discordOptions.Value.Data) &&
            fileSystem.Directory.Exists(discordOptions.Value.Data))
        {
            logger.LogTrace("Using data path {DataPath} from options", discordOptions.Value.Data);
            return fileSystem.DirectoryInfo.New(discordOptions.Value.Data);
        }

        logger.LogTrace("Data path {DataPath} not set or does not exist, evaluating different paths to store data",
            discordOptions.Value.Data);
        var dataPath = environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData).FullName;
        logger.LogTrace("Base path is {DataPath}", dataPath);

        if (fileSystem.Directory.Exists(dataPath))
        {
            var appPath = fileSystem.Path.Combine(dataPath, AppDirectory);
            logger.LogTrace("Data path is {AppPath}", appPath);

            if (fileSystem.Directory.Exists(appPath))
            {
                logger.LogTrace("Path {Path} already exists", appPath);
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

        logger.LogError("Path {DataPath} does not exist or is not accessible", dataPath);
        throw new Exception($"Path {dataPath} does not exist or is not accessible.");
    }
}
