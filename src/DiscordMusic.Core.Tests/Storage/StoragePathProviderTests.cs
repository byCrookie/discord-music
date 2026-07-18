using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Storage;

public class StoragePathProviderTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task StorageDirectoryUsesConfiguredPath(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var environmentVariables = new TestEnvironmentVariables();
        var provider = CreateProvider(
            new StorageOptions { Path = "/configured", MaxSize = "5GB" },
            fileSystem,
            environmentVariables
        );

        var directory = provider.StorageDirectory();

        await Assert
            .That(directory.FullName)
            .IsEqualTo(fileSystem.DirectoryInfo.New("/configured").FullName);
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task StorageDirectoryUsesXdgCacheHomeWhenPathIsNotConfigured(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var xdgCacheHome = fileSystem.DirectoryInfo.New("/xdg-cache").FullName;
        var environmentVariables = new TestEnvironmentVariables(
            new Dictionary<string, string?> { ["XDG_CACHE_HOME"] = xdgCacheHome }
        );

        var provider = CreateProvider(
            new StorageOptions { Path = null, MaxSize = "5GB" },
            fileSystem,
            environmentVariables
        );

        var directory = provider.StorageDirectory();

        await Assert
            .That(directory.FullName)
            .IsEqualTo(
                fileSystem
                    .DirectoryInfo.New(
                        fileSystem.Path.Combine(xdgCacheHome, "bycrookie", "discord-music")
                    )
                    .FullName
            );
    }

    private static StoragePathProvider CreateProvider(
        StorageOptions options,
        MockFileSystem fileSystem,
        IEnvironmentVariables environmentVariables
    )
    {
        return new StoragePathProvider(
            NullLogger<StoragePathProvider>.Instance,
            Options.Create(options),
            fileSystem,
            environmentVariables
        );
    }
}
