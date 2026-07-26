using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Abstractions;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Conversion;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Downloading;

internal sealed class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IFileSystem fileSystem,
    IYouTubeAudioDownloader audioDownloader,
    IAudioConverter audioConverter
) : IYouTubeDownload
{
    private static readonly Counter<long> YouTubeDownloadPipelineRuns =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.youtube.download.pipeline.runs",
            unit: "1",
            description: "YouTube audio download and conversion pipeline runs."
        );

    public async Task<ErrorOr<Success>> DownloadAsync(
        string query,
        IFileInfo output,
        CancellationToken ct
    )
    {
        var result = "completed";
        using var activity = DiscordMusicObservability.StartActivity("youtube.download.pipeline");
        DiscordMusicObservability.SetTag(activity, "music.search.query.length", query.Length);
        var tempFile = fileSystem.FileInfo.New($"{output.FullName}.tmp");
        IFileInfo? downloadedFile = null;

        try
        {
            var download = await audioDownloader.DownloadAsync(query, tempFile, ct);
            if (download.IsError)
            {
                result = "download_failed";
                activity?.SetStatus(ActivityStatusCode.Error, result);
                return download.Errors;
            }

            downloadedFile = download.Value;
            var conversion = await audioConverter.ConvertToPcmAsync(downloadedFile, output, ct);
            if (conversion.IsError)
            {
                result = "conversion_failed";
                activity?.SetStatus(ActivityStatusCode.Error, result);
                return conversion.Errors;
            }

            if (!output.Exists())
            {
                result = "missing_output";
                activity?.SetStatus(ActivityStatusCode.Error, result);
                logger.LogError(
                    "YouTube download did not produce the expected output file. Query={Query} Output={Output}",
                    query,
                    output.FullName
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DownloadOutputMissing",
                        description: "The download completed but the expected PCM output file was not created."
                    )
                    .WithMetadata(
                        ErrorExtensions.MetadataKeys.Operation,
                        "youtube.download.pipeline"
                    )
                    .WithMetadata("query", query)
                    .WithMetadata("output", output.FullName);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.LogInformation(
                "YouTube download succeeded. Query={Query} Output={Output}",
                query,
                output.FullName
            );
            return Result.Success;
        }
        catch (OperationCanceledException)
        {
            result = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Error, result);
            throw;
        }
        catch (Exception ex)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            YouTubeDownloadPipelineRuns.Add(1, new KeyValuePair<string, object?>("result", result));

            if (fileSystem.File.Exists(tempFile.FullName))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", tempFile.FullName);
                fileSystem.File.Delete(tempFile.FullName);
            }

            if (downloadedFile is not null && fileSystem.File.Exists(downloadedFile.FullName))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", downloadedFile.FullName);
                fileSystem.File.Delete(downloadedFile.FullName);
            }
        }
    }
}
