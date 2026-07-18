using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Utils;

public class BinaryLocatorTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LocateAndValidateReturnsRuntimeLocationForEmptyPath(SimulationMode mode)
    {
        var locator = CreateLocator(FileSystemTestData.CreateFileSystem(mode));

        var result = locator.LocateAndValidate(null, "tool");

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Value.Type).IsEqualTo(BinaryLocator.LocationType.Runtime);
        await Assert.That(result.Value.PathToFile).IsEqualTo("tool");
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LocateAndValidateTrimsConfiguredFilePath(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var toolDirectory = fileSystem.DirectoryInfo.New("/tools").FullName;
        fileSystem.Directory.CreateDirectory(toolDirectory);
        var binaryPath = fileSystem.Path.Combine(toolDirectory, "tool");
        await fileSystem.File.WriteAllTextAsync(binaryPath, string.Empty);
        var locator = CreateLocator(fileSystem);

        var result = locator.LocateAndValidate($"  {binaryPath}  ", "tool");

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Value.Type).IsEqualTo(BinaryLocator.LocationType.Resolved);
        await Assert.That(result.Value.PathToFile).IsEqualTo(binaryPath);
        await Assert.That(result.Value.PathToFolder).IsEqualTo(toolDirectory);
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LocateAndValidateTrimsConfiguredDirectoryPath(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var toolDirectory = fileSystem.DirectoryInfo.New("/tools").FullName;
        fileSystem.Directory.CreateDirectory(toolDirectory);
        var binaryPath = fileSystem.Path.Combine(toolDirectory, "tool");
        await fileSystem.File.WriteAllTextAsync(binaryPath, string.Empty);
        var locator = CreateLocator(fileSystem);

        var result = locator.LocateAndValidate($"  {toolDirectory}  ", "tool");

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Value.Type).IsEqualTo(BinaryLocator.LocationType.Resolved);
        await Assert.That(result.Value.PathToFile).IsEqualTo(binaryPath);
        await Assert.That(result.Value.PathToFolder).IsEqualTo(toolDirectory);
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LocateAndValidateReturnsErrorForDirectoryWithoutBinary(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var toolDirectory = fileSystem.DirectoryInfo.New("/tools").FullName;
        fileSystem.Directory.CreateDirectory(toolDirectory);
        var locator = CreateLocator(fileSystem);

        var result = locator.LocateAndValidate(toolDirectory, "tool");

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.FirstError.Code).IsEqualTo("Binary.NotFound");
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LocateAndValidateReflectsFileSystemChanges(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var toolDirectory = fileSystem.DirectoryInfo.New("/tools").FullName;
        fileSystem.Directory.CreateDirectory(toolDirectory);
        var binaryPath = fileSystem.Path.Combine(toolDirectory, "tool");
        await fileSystem.File.WriteAllTextAsync(binaryPath, string.Empty);
        var locator = CreateLocator(fileSystem);

        var firstResult = locator.LocateAndValidate($"  {toolDirectory}  ", "tool");
        fileSystem.File.Delete(binaryPath);
        var secondResult = locator.LocateAndValidate(toolDirectory, "tool");

        await Assert.That(firstResult.IsError).IsFalse();
        await Assert.That(secondResult.IsError).IsTrue();
        await Assert.That(secondResult.FirstError.Code).IsEqualTo("Binary.NotFound");
    }

    private static BinaryLocator CreateLocator(IFileSystem fileSystem)
    {
        return new BinaryLocator(fileSystem, NullLogger<BinaryLocator>.Instance);
    }
}
