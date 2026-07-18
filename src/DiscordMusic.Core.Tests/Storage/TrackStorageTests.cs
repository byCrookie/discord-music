using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils.Json;
using Flurl;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Storage;

public class TrackStorageTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task GetTrackPathUsesProvidedExtension(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var storagePath = fileSystem.DirectoryInfo.New("/storage").FullName;
        var storage = CreateStorage(storagePath, fileSystem);

        var path = storage.GetTrackPath(CreateTrack(), "opus");

        await Assert
            .That(path.FullName)
            .IsEqualTo(fileSystem.Path.Combine(storagePath, "tracks", "track-id.opus"));
    }

    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task GetTrackPathRejectsEmptyExtension(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var storage = CreateStorage(fileSystem.DirectoryInfo.New("/storage").FullName, fileSystem);

        await Assert.ThrowsAsync<ArgumentException>(() =>
        {
            storage.GetTrackPath(CreateTrack(), " ");
            return Task.CompletedTask;
        });
    }

    private static TrackStorage CreateStorage(string storagePath, MockFileSystem fileSystem)
    {
        var storagePathProvider = Substitute.For<IStoragePathProvider>();
        storagePathProvider.StorageDirectory().Returns(fileSystem.DirectoryInfo.New(storagePath));

        return new TrackStorage(
            fileSystem,
            storagePathProvider,
            Substitute.For<IJsonSerializer>(),
            NullLogger<TrackStorage>.Instance
        );
    }

    private static Track CreateTrack()
    {
        return new Track(
            "track-id",
            "Track",
            "Artist",
            new Url("https://www.youtube.com/watch?v=track-id"),
            TimeSpan.FromMinutes(3)
        );
    }
}
