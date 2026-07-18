using System.Diagnostics;
using System.IO.Abstractions;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Utils;

internal sealed class BinaryLocator(IFileSystem fileSystem, ILogger<BinaryLocator> logger)
{
    public enum LocationType
    {
        Runtime,
        Resolved,
    }

    public ErrorOr<BinaryLocation> LocateAndValidate(string? path, string defaultBinaryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultBinaryName);
        var configuredPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

        switch (configuredPath)
        {
            case null:
                logger.LogTrace(
                    "{DefaultBinaryName} will be resolved at runtime by the operating system.",
                    defaultBinaryName
                );
                return BinaryLocation.ForRuntime(defaultBinaryName);
            case ".":
            {
                var currentDirectory = fileSystem.Directory.GetCurrentDirectory();
                var currentDirectoryBinaryPath = ResolveBinaryPath(
                    currentDirectory,
                    defaultBinaryName
                );

                logger.LogTrace(
                    "Path is '.', returning current directory for binary {DefaultBinaryName}.",
                    defaultBinaryName
                );

                if (currentDirectoryBinaryPath is not null)
                {
                    var currentDirectoryLocation = BinaryLocation.ForResolved(
                        fileSystem.DirectoryInfo.New(currentDirectory),
                        fileSystem.FileInfo.New(currentDirectoryBinaryPath),
                        defaultBinaryName
                    );

                    logger.LogTrace(
                        "Binary {DefaultBinaryName} found in current directory {CurrentDirectory}.",
                        defaultBinaryName,
                        currentDirectoryLocation.PathToFolder
                    );
                    return currentDirectoryLocation;
                }

                logger.LogError(
                    "Binary not found in current directory. BinaryName={BinaryName} CurrentDirectory={CurrentDirectory}",
                    defaultBinaryName,
                    currentDirectory
                );

                return BinaryNotFound(defaultBinaryName)
                    .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "binary.locate")
                    .WithMetadata("binaryName", defaultBinaryName)
                    .WithMetadata("path", configuredPath)
                    .WithMetadata("currentDirectory", currentDirectory);
            }
        }

        if (fileSystem.File.Exists(configuredPath))
        {
            var configuredBinary = fileSystem.FileInfo.New(configuredPath);

            logger.LogTrace(
                "Path is a file, returning directory of file for binary {DefaultBinaryName}.",
                defaultBinaryName
            );
            return BinaryLocation.ForResolved(
                fileSystem.DirectoryInfo.New(
                    fileSystem.Path.GetDirectoryName(configuredBinary.FullName)
                        ?? fileSystem.Directory.GetCurrentDirectory()
                ),
                configuredBinary,
                configuredBinary.Name
            );
        }

        if (fileSystem.Directory.Exists(configuredPath))
        {
            var directoryBinaryPath = ResolveBinaryPath(configuredPath, defaultBinaryName);
            if (directoryBinaryPath is not null)
            {
                logger.LogTrace(
                    "Binary {DefaultBinaryName} found in directory {Path}.",
                    defaultBinaryName,
                    configuredPath
                );
                return BinaryLocation.ForResolved(
                    fileSystem.DirectoryInfo.New(configuredPath),
                    fileSystem.FileInfo.New(directoryBinaryPath),
                    defaultBinaryName
                );
            }

            logger.LogError(
                "Binary not found in directory. BinaryName={BinaryName} Directory={Directory}",
                defaultBinaryName,
                configuredPath
            );

            return BinaryNotFound(defaultBinaryName)
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "binary.locate")
                .WithMetadata("binaryName", defaultBinaryName)
                .WithMetadata("path", configuredPath)
                .WithMetadata("directory", configuredPath);
        }

        logger.LogError(
            "Invalid binary path configuration. BinaryName={BinaryName} Path={Path}",
            defaultBinaryName,
            configuredPath
        );

        return Error
            .Unexpected(
                code: "Binary.InvalidPath",
                description: $"Invalid configuration for `{defaultBinaryName}`."
            )
            .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "binary.locate")
            .WithMetadata("binaryName", defaultBinaryName)
            .WithMetadata("path", configuredPath);
    }

    private string? ResolveBinaryPath(string directory, string defaultBinaryName)
    {
        return BinaryNameCandidates(defaultBinaryName)
            .Select(candidate => fileSystem.Path.Combine(directory, candidate))
            .FirstOrDefault(binaryPath => fileSystem.File.Exists(binaryPath));
    }

    private IEnumerable<string> BinaryNameCandidates(string defaultBinaryName)
    {
        yield return defaultBinaryName;

        if (
            OperatingSystem.IsWindows()
            && string.IsNullOrEmpty(fileSystem.Path.GetExtension(defaultBinaryName))
        )
        {
            yield return $"{defaultBinaryName}.exe";
        }
    }

    private static Error BinaryNotFound(string defaultBinaryName)
    {
        return Error.Unexpected(
            code: "Binary.NotFound",
            description: $"Required tool `{defaultBinaryName}` wasn't found. Check your configuration."
        );
    }

    public abstract record BinaryLocation(LocationType Type, string BinaryName)
    {
        public abstract string PathToFile { get; }

        public virtual string PathToFolder =>
            throw new UnreachableException(
                $"Cannot get folder path for runtime binary {BinaryName}."
            );

        public static BinaryLocation ForRuntime(string binaryName)
        {
            return new RuntimeLocation(binaryName);
        }

        public static BinaryLocation ForResolved(
            IDirectoryInfo directory,
            IFileInfo binary,
            string binaryName
        )
        {
            return new ResolvedLocation(directory, binary, binaryName);
        }

        private sealed record RuntimeLocation(string BinaryName)
            : BinaryLocation(LocationType.Runtime, BinaryName)
        {
            public override string PathToFile => BinaryName;
        }

        private sealed record ResolvedLocation(
            IDirectoryInfo Directory,
            IFileInfo Binary,
            string BinaryName
        ) : BinaryLocation(LocationType.Resolved, BinaryName)
        {
            public override string PathToFile => Binary.FullName;
            public override string PathToFolder => Directory.FullName;
        }
    }
}
