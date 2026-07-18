using DiscordMusic.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Storage;

public class StorageCacheTrimmerTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task TrimAsyncDeletesOldestNonMetadataFilesUntilBelowLimit(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var storagePath = fileSystem.DirectoryInfo.New("/storage").FullName;
        fileSystem.Directory.CreateDirectory(storagePath);

        var oldFile = fileSystem.Path.Combine(storagePath, "old.pcm");
        var newFile = fileSystem.Path.Combine(storagePath, "new.pcm");
        var metadataFile = fileSystem.Path.Combine(storagePath, "track.json");

        await fileSystem.File.WriteAllBytesAsync(oldFile, new byte[100]);
        await fileSystem.File.WriteAllBytesAsync(newFile, new byte[100]);
        await fileSystem.File.WriteAllBytesAsync(metadataFile, new byte[100]);

        fileSystem.File.SetLastAccessTimeUtc(oldFile, DateTime.UtcNow.AddMinutes(-10));
        fileSystem.File.SetLastAccessTimeUtc(newFile, DateTime.UtcNow);
        fileSystem.File.SetLastAccessTimeUtc(metadataFile, DateTime.UtcNow.AddMinutes(-20));

        var trimmer = new StorageCacheTrimmer(fileSystem, NullLogger<StorageCacheTrimmer>.Instance);

        await trimmer.TrimAsync(storagePath, 100, CancellationToken.None);

        await Assert.That(fileSystem.File.Exists(oldFile)).IsFalse();
        await Assert.That(fileSystem.File.Exists(newFile)).IsTrue();
        await Assert.That(fileSystem.File.Exists(metadataFile)).IsTrue();
    }
}
