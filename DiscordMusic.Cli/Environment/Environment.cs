using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Environment;

internal class Environment(IFileSystem fileSystem, ILogger<Environment> logger) : IEnvironment
{
    public IDirectoryInfo GetFolderPath(System.Environment.SpecialFolder folder)
    {
        var folderPath = System.Environment.GetFolderPath(folder);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            var currentDirectory = fileSystem.Directory.GetCurrentDirectory();
            logger.LogWarning("Unable to get path for {folder}, using current directory {currentDirectory} instead",
                folder,
                currentDirectory);
            return fileSystem.DirectoryInfo.New(currentDirectory);
        }

        var path = fileSystem.DirectoryInfo.New(folderPath);
        logger.LogDebug("Path for {folder} is {path}", folder, path.FullName);
        return path;
    }
}
