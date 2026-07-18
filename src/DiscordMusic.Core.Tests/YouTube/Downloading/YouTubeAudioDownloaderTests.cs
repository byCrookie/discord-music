using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using DiscordMusic.Core.YouTube.Downloading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.YouTube.Downloading;

public class YouTubeAudioDownloaderTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task DownloadAsyncDeclaresOpusOutputTemplate(SimulationMode mode)
    {
        var commandRunner = Substitute.For<ICliCommandRunner>();
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var options = new YouTubeOptions();
        var downloader = new YouTubeAudioDownloader(
            NullLogger<YouTubeAudioDownloader>.Instance,
            Options.Create(options),
            CreateToolLocations(fileSystem, options),
            commandRunner,
            new TestEnvironmentVariables()
        );
        var outputBase = fileSystem.FileInfo.New("/downloads/output.tmp");
        IReadOnlyList<string>? arguments = null;
        commandRunner
            .RunAsync(
                Arg.Any<string>(),
                Arg.Do<IReadOnlyList<string>>(value => arguments = value),
                Arg.Any<IReadOnlyDictionary<string, string?>?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new CliCommandResult(0, string.Empty, string.Empty));

        var result = await downloader.DownloadAsync(
            "https://example.com/video",
            outputBase,
            CancellationToken.None
        );

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Value.FullName).IsEqualTo($"{outputBase.FullName}.opus");
        await Assert.That(arguments).IsNotNull();
        await Assert.That(arguments!).Contains("--audio-format");
        await Assert.That(arguments!).Contains("opus");
        await Assert.That(arguments!).Contains("--output");
        await Assert.That(arguments!).Contains($"{outputBase.FullName}.%(ext)s");
    }

    private static YouTubeToolLocations CreateToolLocations(
        MockFileSystem fileSystem,
        YouTubeOptions options
    )
    {
        var toolLocations = new YouTubeToolLocations(
            new BinaryLocator(fileSystem, NullLogger<BinaryLocator>.Instance)
        );
        toolLocations.Load(options);
        return toolLocations;
    }
}
