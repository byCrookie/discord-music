using DiscordMusic.Core.Discord.CommandSupport;
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
        if (
            !trackQueue.TryUpdateStatus(
                request.Origin.GuildId,
                request.Track.Id,
                QueuedTrackStatus.Downloading
            )
        )
        {
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
            logger.LogInformation(
                "YouTube download completed. GuildId={GuildId}, TrackId={TrackId}, Title={Title}, Output={Output}",
                request.Origin.GuildId,
                request.Track.Id,
                request.Track.Name,
                outputFile.FullName
            );
            return;
        }

        logger.LogWarning(
            "YouTube download completed but queue item was gone. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
            request.Origin.GuildId,
            request.Track.Id,
            request.Track.Name
        );
    }
}
