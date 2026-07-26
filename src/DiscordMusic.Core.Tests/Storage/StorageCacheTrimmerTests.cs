using DiscordMusic.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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

        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.Zero)
        );
        var now = timeProvider.GetUtcNow().UtcDateTime;
        fileSystem.File.SetLastAccessTimeUtc(oldFile, now.AddMinutes(-10));
        fileSystem.File.SetLastAccessTimeUtc(newFile, now);
        fileSystem.File.SetLastAccessTimeUtc(metadataFile, now.AddMinutes(-20));

        var trimmer = new StorageCacheTrimmer(fileSystem, NullLogger<StorageCacheTrimmer>.Instance);

        await trimmer.TrimAsync(storagePath, 100, CancellationToken.None);

        await Assert.That(fileSystem.File.Exists(oldFile)).IsFalse();
        await Assert.That(fileSystem.File.Exists(newFile)).IsTrue();
        await Assert.That(fileSystem.File.Exists(metadataFile)).IsTrue();
    }
}
