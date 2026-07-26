using System.Diagnostics;
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
        var result = "no_pending";
        using var activity = DiscordMusicObservability.StartActivity("youtube.download.schedule");
        DiscordMusicObservability.SetGuildTag(activity, guildId);

        try
        {
            if (!trackQueue.TryMarkNextPendingAsDownloading(guildId, out var queuedTrack))
            {
                return;
            }

            if (queuedTrack is not { Origin: { } origin } item)
            {
                if (queuedTrack is { } trackWithoutOrigin)
                {
                    DiscordMusicObservability.SetTag(
                        activity,
                        "music.track.id",
                        trackWithoutOrigin.Track.Id
                    );
                    trackQueue.TryUpdateStatus(
                        guildId,
                        trackWithoutOrigin.Track.Id,
                        QueuedTrackStatus.Pending
                    );
                }

                result = "missing_origin";
                logger.LogWarning(
                    "Cannot queue lazy download for track without request origin. GuildId={GuildId}, Track={Track}",
                    guildId,
                    queuedTrack?.Track
                );
                return;
            }

            DiscordMusicObservability.SetTag(activity, "music.track.id", item.Track.Id);
            if (trackStorage.IsTrackCached(item.Track, "pcm"))
            {
                DiscordMusicObservability.RecordTrackCacheLookup(guildId, "scheduler", hit: true);
                if (trackQueue.TryUpdateStatus(guildId, item.Track.Id, QueuedTrackStatus.Available))
                {
                    result = "cached_available";
                    logger.LogInformation(
                        "Skipping lazy download because track is already cached. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                        guildId,
                        item.Track.Id,
                        item.Track.Name
                    );
                }
                else
                {
                    result = "cached_stale";
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
                result = "queued";
                return;
            }

            result = "queue_full";
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
        catch (Exception ex)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            DiscordMusicObservability.SetTag(activity, "result", result);
            if (result != "exception")
            {
                activity?.SetStatus(ActivityStatusCode.Ok, result);
            }
        }
    }
}
