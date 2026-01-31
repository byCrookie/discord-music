using System.Diagnostics;
using System.IO.Abstractions;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Utils;

public class BinaryLocator(IFileSystem fileSystem, ILogger<BinaryLocator> logger)
{
    public enum LocationType
    {
        Runtime,
        Resolved,
    }

    public ErrorOr<BinaryLocation> LocateAndValidate(string? path, string defaultBinaryName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogTrace(
                "{DefaultBinaryName} will be resolved at runtime by the system by the os.",
                defaultBinaryName
            );
            return new BinaryLocation(LocationType.Runtime, null, null, defaultBinaryName);
        }

        if (path.Trim() == ".")
        {
            logger.LogTrace(
                "Path is '.', returning current directory for binary {DefaultBinaryName}.",
                defaultBinaryName
            );
            var current = new BinaryLocation(
                LocationType.Resolved,
                fileSystem.DirectoryInfo.New(fileSystem.Directory.GetCurrentDirectory()),
                fileSystem.FileInfo.New(
                    fileSystem.Path.Combine(
                        fileSystem.Directory.GetCurrentDirectory(),
                        defaultBinaryName
                    )
                ),
                defaultBinaryName
            );

            if (fileSystem.File.Exists(current.Binary!.FullName))
            {
                logger.LogTrace(
                    "Binary {DefaultBinaryName} found in current directory {CurrentDirectory}.",
                    defaultBinaryName,
                    current.Directory!.FullName
                );
                return current;
            }

            logger.LogError(
                "Binary {DefaultBinaryName} not found in current directory {CurrentDirectory}.",
                defaultBinaryName,
                current.Directory!.FullName
            );
            return Error.Unexpected(
                description: $"Required tool `{defaultBinaryName}` wasn't found. Check your configuration."
            );
        }

        if (fileSystem.File.Exists(path))
        {
            logger.LogTrace(
                "Path is a file, returning directory of file for binary {DefaultBinaryName}.",
                defaultBinaryName
            );
            return new BinaryLocation(
                LocationType.Resolved,
                fileSystem.DirectoryInfo.New(
                    fileSystem.Path.GetDirectoryName(path)
                        ?? fileSystem.Directory.GetCurrentDirectory()
                ),
                fileSystem.FileInfo.New(path),
                fileSystem.Path.GetFileName(path)
            );
        }

        if (fileSystem.Directory.Exists(path))
        {
            var binaryPath = fileSystem.Path.Combine(path, defaultBinaryName);
            if (fileSystem.File.Exists(binaryPath))
            {
                logger.LogTrace(
                    "Binary {DefaultBinaryName} found in directory {Path}.",
                    defaultBinaryName,
                    path
                );
                return new BinaryLocation(
                    LocationType.Resolved,
                    fileSystem.DirectoryInfo.New(path),
                    fileSystem.FileInfo.New(binaryPath),
                    defaultBinaryName
                );
            }

            logger.LogError(
                "Binary {DefaultBinaryName} not found in directory {Path}.",
                defaultBinaryName,
                path
            );
            return Error.Unexpected(
                description: $"Required tool `{defaultBinaryName}` wasn't found. Check your configuration."
            );
        }

        logger.LogError(
            "Path {Path} is not a valid path or directory for binary {DefaultBinaryName}.",
            path,
            defaultBinaryName
        );
        return Error.Unexpected(
            description: $"Invalid configuration for `{defaultBinaryName}`."
        );
    }

    public record BinaryLocation(
        LocationType Type,
        IDirectoryInfo? Directory,
        IFileInfo? Binary,
        string BinaryName
    )
    {
        public string PathToFile =>
            Type switch
            {
                LocationType.Runtime => BinaryName,
                LocationType.Resolved => Binary!.FullName,
                _ => throw new ArgumentOutOfRangeException(nameof(Type)),
            };

        public string PathToFolder =>
            Type switch
            {
                LocationType.Runtime => throw new UnreachableException(
                    $"Cannot get folder path for runtime binary {BinaryName}."
                ),
                LocationType.Resolved => Directory!.FullName,
                _ => throw new ArgumentOutOfRangeException(nameof(Type)),
            };
    }
}
