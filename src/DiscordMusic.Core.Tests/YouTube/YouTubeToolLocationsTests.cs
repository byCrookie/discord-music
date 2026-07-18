using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.YouTube;

public class YouTubeToolLocationsTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task LoadStoresResolvedLocationsForReuse(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var toolDirectory = fileSystem.DirectoryInfo.New("/tools").FullName;
        fileSystem.Directory.CreateDirectory(toolDirectory);
        var ffmpegPath = fileSystem.Path.Combine(toolDirectory, "ffmpeg");
        await fileSystem.File.WriteAllTextAsync(ffmpegPath, string.Empty);
        await fileSystem.File.WriteAllTextAsync(
            fileSystem.Path.Combine(toolDirectory, "deno"),
            string.Empty
        );
        await fileSystem.File.WriteAllTextAsync(
            fileSystem.Path.Combine(toolDirectory, "yt-dlp"),
            string.Empty
        );
        var toolLocations = new YouTubeToolLocations(
            new BinaryLocator(fileSystem, NullLogger<BinaryLocator>.Instance)
        );

        var loadResult = toolLocations.Load(
            new YouTubeOptions
            {
                Ffmpeg = toolDirectory,
                Deno = toolDirectory,
                Ytdlp = toolDirectory,
            }
        );
        fileSystem.File.Delete(ffmpegPath);

        await Assert.That(loadResult.Ffmpeg.IsError).IsFalse();
        await Assert.That(toolLocations.Value.Ffmpeg.PathToFile).IsEqualTo(ffmpegPath);
    }
}
