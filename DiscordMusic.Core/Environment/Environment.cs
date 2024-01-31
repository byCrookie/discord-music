using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Environment;

internal class Environment(IFileSystem fileSystem, ILogger<Environment> logger) : IEnvironment
{
    public IDirectoryInfo GetFolderPath(System.Environment.SpecialFolder folder)
    {
        var path = fileSystem.DirectoryInfo.New(System.Environment.GetFolderPath(folder));
        logger.LogDebug("Path for {folder} is {path}", folder, path.FullName);
        return path;
    }
}