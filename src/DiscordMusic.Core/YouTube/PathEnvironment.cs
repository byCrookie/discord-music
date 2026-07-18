using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.YouTube;

internal static class PathEnvironment
{
    public static IReadOnlyDictionary<string, string?>? ForPrependedDirectory(
        BinaryLocator.BinaryLocation location,
        IEnvironmentVariables environmentVariables,
        IFileSystem fileSystem
    )
    {
        if (location.Type != BinaryLocator.LocationType.Resolved)
        {
            return null;
        }

        const string pathKey = "PATH";
        var existingPath = environmentVariables.GetVariable(pathKey) ?? string.Empty;
        var directory = location.PathToFolder;
        var segments = existingPath.Split(
            fileSystem.Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

        if (segments.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = string.IsNullOrWhiteSpace(existingPath)
            ? directory
            : string.Join(fileSystem.Path.PathSeparator, new[] { directory, existingPath });

        return new Dictionary<string, string?> { [pathKey] = path };
    }
}
