using System.Diagnostics;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Downloading;

internal sealed class YouTubeDownloadRequestProcessor(
    ILogger<YouTubeDownloadRequestProcessor> logger,
    IDiscordFeedbackService feedback,
    IYouTubeDownload youTubeDownload,
    ITrackQueue trackQueue,
    ITrackStorage trackStorage
) : IYouTubeDownloadRequestProcessor
{
    public async Task ProcessAsync(
        YouTubeDownloadRequest request,
        CancellationToken cancellationToken
    )
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = DiscordMusicObservability.StartActivity("youtube.download.process");
        DiscordMusicObservability.SetGuildTag(activity, request.Origin.GuildId);
        DiscordMusicObservability.SetTag(activity, "music.track.id", request.Track.Id);
        DiscordMusicObservability.SetTag(
            activity,
            "music.track.duration_ms",
            request.Track.Duration.TotalMilliseconds
        );
        var metricTags = DiscordMusicObservability.GuildTags(request.Origin.GuildId);

        if (
            !trackQueue.TryUpdateStatus(
                request.Origin.GuildId,
                request.Track.Id,
                QueuedTrackStatus.Downloading
            )
        )
        {
            activity?.SetStatus(ActivityStatusCode.Ok, "stale_request");
            metricTags.Add("result", "stale");
            DiscordMusicObservability.DownloadRequests.Add(1, metricTags);
            DiscordMusicObservability.DownloadDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                metricTags
            );
            logger.LogWarning(
                "Skipping stale YouTube download request because the queue item is gone. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                request.Origin.GuildId,
                request.Track.Id,
                request.Track.Name
            );
            return;
        }

        logger.LogInformation(
            "Starting YouTube download. GuildId={GuildId}, TrackId={TrackId}, Title={Title}, Url={Url}",
            request.Origin.GuildId,
            request.Track.Id,
            request.Track.Name,
            request.Track.Url
        );

        var outputFile = trackStorage.GetTrackPath(request.Track, "pcm");
        var download = await youTubeDownload.DownloadAsync(
            request.Track.Url.ToString(),
            outputFile,
            cancellationToken
        );

        if (!download.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, download.ToErrorContent());
            metricTags.Add("result", "failed");
            DiscordMusicObservability.DownloadRequests.Add(1, metricTags);
            DiscordMusicObservability.DownloadDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                metricTags
            );
            logger.LogWarning(
                "YouTube download failed. GuildId={GuildId}, TrackId={TrackId}, Title={Title}, Error={Error}",
                request.Origin.GuildId,
                request.Track.Id,
                request.Track.Name,
                download.ToErrorContent()
            );
            trackQueue.TryUpdateStatus(
                request.Origin.GuildId,
                request.Track.Id,
                QueuedTrackStatus.Failed
            );
            await feedback.SendPrivateAsync(
                request.Origin,
                $"Download failed for **{request.Track.Name}**. I marked it as failed and will continue with the next queued track.\n{download.ToErrorContent()}",
                cancellationToken
            );
            return;
        }

        if (
            trackQueue.TryUpdateStatus(
                request.Origin.GuildId,
                request.Track.Id,
                QueuedTrackStatus.Available
            )
        )
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            metricTags.Add("result", "completed");
            DiscordMusicObservability.DownloadRequests.Add(1, metricTags);
            DiscordMusicObservability.DownloadDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                metricTags
            );
            logger.LogInformation(
                "YouTube download completed. GuildId={GuildId}, TrackId={TrackId}, Title={Title}, Output={Output}",
                request.Origin.GuildId,
                request.Track.Id,
                request.Track.Name,
                outputFile.FullName
            );
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Ok, "queue_item_gone");
        metricTags.Add("result", "stale_after_download");
        DiscordMusicObservability.DownloadRequests.Add(1, metricTags);
        DiscordMusicObservability.DownloadDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            metricTags
        );
        logger.LogWarning(
            "YouTube download completed but queue item was gone. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
            request.Origin.GuildId,
            request.Track.Id,
            request.Track.Name
        );
    }
}
