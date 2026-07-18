using System.IO.Abstractions;
using DiscordMusic.Core.YouTube.Conversion;
using DiscordMusic.Core.YouTube.Downloading;
using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.YouTube.Downloading;

public class YouTubeDownloadTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task DownloadAsyncPassesTempBaseToDownloaderAndConvertsReturnedFile(
        SimulationMode mode
    )
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var audioDownloader = Substitute.For<IYouTubeAudioDownloader>();
        var audioConverter = Substitute.For<IAudioConverter>();
        var download = new YouTubeDownload(
            NullLogger<YouTubeDownload>.Instance,
            fileSystem,
            audioDownloader,
            audioConverter
        );
        var output = fileSystem.FileInfo.New("/downloads/output.pcm");
        var downloadedFile = fileSystem.FileInfo.New($"{output.FullName}.tmp.opus");
        IFileInfo? outputBase = null;
        IFileInfo? conversionInput = null;
        IFileInfo? conversionOutput = null;
        audioDownloader
            .DownloadAsync(
                Arg.Any<string>(),
                Arg.Do<IFileInfo>(value => outputBase = value),
                Arg.Any<CancellationToken>()
            )
            .Returns(ErrorOrFactory.From(downloadedFile));
        audioConverter
            .ConvertToPcmAsync(
                Arg.Do<IFileInfo>(value => conversionInput = value),
                Arg.Do<IFileInfo>(value => conversionOutput = value),
                Arg.Any<CancellationToken>()
            )
            .Returns(Result.Success);

        var result = await download.DownloadAsync(
            "https://example.com/video",
            output,
            CancellationToken.None
        );

        await Assert.That(result.IsError).IsFalse();
        await Assert.That(outputBase?.FullName).IsEqualTo($"{output.FullName}.tmp");
        await Assert.That(conversionInput?.FullName).IsEqualTo(downloadedFile.FullName);
        await Assert.That(conversionOutput?.FullName).IsEqualTo(output.FullName);
    }
}
