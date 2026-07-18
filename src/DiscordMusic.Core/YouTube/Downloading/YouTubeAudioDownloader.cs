using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube.Downloading;

internal sealed class YouTubeAudioDownloader(
    ILogger<YouTubeAudioDownloader> logger,
    IOptions<YouTubeOptions> options,
    YouTubeToolLocations toolLocations,
    ICliCommandRunner commandRunner,
    IEnvironmentVariables environmentVariables
) : IYouTubeAudioDownloader
{
    private const string AudioFormat = "opus";

    public async Task<ErrorOr<IFileInfo>> DownloadAsync(
        string query,
        IFileInfo outputBase,
        CancellationToken ct
    )
    {
        var outputTemplate = outputBase.FileSystem.FileInfo.New($"{outputBase.FullName}.%(ext)s");
        var outputFile = outputBase.FileSystem.FileInfo.New($"{outputBase.FullName}.{AudioFormat}");
        var loadedLocations = toolLocations.Value;

        var arguments = new List<string>
        {
            "--default-search",
            "auto",
            query,
            "-f",
            "bestaudio",
            "--extract-audio",
            "--audio-format",
            AudioFormat,
            "--audio-quality",
            "0",
            "--output",
            outputTemplate.FullName,
            "--no-playlist",
        };

        if (loadedLocations.Ffmpeg.Type == BinaryLocator.LocationType.Resolved)
        {
            arguments.Add("--ffmpeg-location");
            arguments.Add(loadedLocations.Ffmpeg.PathToFolder);
        }

        arguments.AddRange(YtdlpArgumentWriter.RuntimeArguments(options.Value));

        var environment = PathEnvironment.ForPrependedDirectory(
            loadedLocations.Deno,
            environmentVariables,
            outputBase.FileSystem
        );
        var result = await commandRunner.RunAsync(
            loadedLocations.Ytdlp.PathToFile,
            arguments,
            environment,
            ct
        );

        if (result.ExitCode == 0)
        {
            logger.LogDebug(
                "Downloaded YouTube audio. Query={Query} Output={Output}",
                query,
                outputFile.FullName
            );
            return ErrorOrFactory.From(outputFile);
        }

        logger.LogError(
            "YouTube download failed. ExitCode={ExitCode} Query={Query} Output={Output} Error={Error}",
            result.ExitCode,
            query,
            outputTemplate.FullName,
            result.StandardError
        );

        return Error
            .Unexpected(
                code: "YouTube.DownloadFailed",
                description: "Downloading from YouTube failed."
            )
            .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "youtube.download")
            .WithMetadata("exitCode", result.ExitCode)
            .WithMetadata("stderr", result.StandardError)
            .WithMetadata("query", query)
            .WithMetadata("output", outputTemplate.FullName);
    }
}
