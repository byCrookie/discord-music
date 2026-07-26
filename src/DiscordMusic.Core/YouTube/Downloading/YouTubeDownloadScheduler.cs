using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Downloading;

internal sealed class YouTubeDownloadScheduler(
    ILogger<YouTubeDownloadScheduler> logger,
    ITrackQueue trackQueue,
    IBackgroundQueue<YouTubeDownloadRequest> downloadQueue,
    IDiscordFeedbackService feedback,
    ITrackStorage trackStorage
) : IYouTubeDownloadScheduler
{
    public async Task EnsureNextTrackQueuedAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!trackQueue.TryMarkNextPendingAsDownloading(guildId, out var queuedTrack))
        {
            return;
        }

        if (queuedTrack is not { Origin: { } origin } item)
        {
            if (queuedTrack is { } trackWithoutOrigin)
            {
                trackQueue.TryUpdateStatus(
                    guildId,
                    trackWithoutOrigin.Track.Id,
                    QueuedTrackStatus.Pending
                );
            }

            logger.LogWarning(
                "Cannot queue lazy download for track without request origin. GuildId={GuildId}, Track={Track}",
                guildId,
                queuedTrack?.Track
            );
            return;
        }

        if (trackStorage.IsTrackCached(item.Track, "pcm"))
        {
            DiscordMusicObservability.RecordTrackCacheLookup(guildId, "scheduler", hit: true);
            if (trackQueue.TryUpdateStatus(guildId, item.Track.Id, QueuedTrackStatus.Available))
            {
                logger.LogInformation(
                    "Skipping lazy download because track is already cached. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                    guildId,
                    item.Track.Id,
                    item.Track.Name
                );
            }
            else
            {
                logger.LogWarning(
                    "Lazy download found cached track but queue item was gone. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                    guildId,
                    item.Track.Id,
                    item.Track.Name
                );
            }

            return;
        }

        DiscordMusicObservability.RecordTrackCacheLookup(guildId, "scheduler", hit: false);
        logger.LogInformation(
            "Queueing lazy download for next track. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
            guildId,
            item.Track.Id,
            item.Track.Name
        );

        var queued = await downloadQueue.QueueAsync(_ => new YouTubeDownloadRequest(
            item.Track,
            origin
        ));

        if (queued)
        {
            return;
        }

        trackQueue.TryUpdateStatus(guildId, item.Track.Id, QueuedTrackStatus.Pending);
        logger.LogWarning(
            "Lazy download queue rejected track. It will remain pending. GuildId={GuildId}, TrackId={TrackId}",
            guildId,
            item.Track.Id
        );
        await feedback.SendPrivateAsync(
            origin,
            $"I could not schedule the download for **{item.Track.Name}** because the download queue is full. It will stay queued and I will try again later.",
            cancellationToken
        );
    }
}
