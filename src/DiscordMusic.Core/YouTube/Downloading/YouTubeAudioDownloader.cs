using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Observability;
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
    private static readonly Counter<long> AudioDownloads =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.youtube.audio.downloads",
            unit: "1",
            description: "Low-level yt-dlp audio download runs."
        );

    public async Task<ErrorOr<IFileInfo>> DownloadAsync(
        string query,
        IFileInfo outputBase,
        CancellationToken ct
    )
    {
        using var activity = DiscordMusicObservability.StartActivity(
            "youtube.audio.download",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetTag(activity, "music.search.query.length", query.Length);
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
        var metricResult = "completed";
        try
        {
            var result = await commandRunner.RunAsync(
                loadedLocations.Ytdlp.PathToFile,
                arguments,
                environment,
                ct
            );

            if (result.ExitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                DiscordMusicObservability.SetTag(activity, "audio.format", AudioFormat);
                logger.LogDebug(
                    "Downloaded YouTube audio. Query={Query} Output={Output}",
                    query,
                    outputFile.FullName
                );
                return ErrorOrFactory.From(outputFile);
            }

            metricResult = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, "non_zero_exit_code");
            DiscordMusicObservability.SetTag(activity, "process.exit_code", result.ExitCode);
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
        catch (OperationCanceledException)
        {
            metricResult = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Error, metricResult);
            throw;
        }
        catch (Exception ex)
        {
            metricResult = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            AudioDownloads.Add(1, Tags(metricResult));
        }
    }

    private static TagList Tags(string result)
    {
        return new TagList { { "audio.format", AudioFormat }, { "result", result } };
    }
}
