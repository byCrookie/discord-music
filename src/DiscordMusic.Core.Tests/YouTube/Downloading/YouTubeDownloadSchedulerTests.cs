using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.YouTube.Downloading;
using Flurl;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DiscordMusic.Core.Tests.YouTube.Downloading;

public class YouTubeDownloadSchedulerTests
{
    private const ulong GuildId = 1;

    [Test]
    public async Task EnsureNextTrackQueuedAsyncMarksCachedPendingTrackAvailable()
    {
        var track = new Track(
            "cached",
            "Cached Track",
            "Artist",
            new Url("https://www.youtube.com/watch?v=cached"),
            TimeSpan.FromMinutes(3)
        );
        var trackQueue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        trackQueue.EnqueueLast(
            GuildId,
            new QueuedTrack(
                track,
                QueuedTrackStatus.Pending,
                new DiscordRequestOrigin(GuildId, 2, 3)
            )
        );
        var downloadQueue = Substitute.For<IBackgroundQueue<YouTubeDownloadRequest>>();
        var trackStorage = Substitute.For<ITrackStorage>();
        trackStorage.IsTrackCached(track, "pcm").Returns(true);
        var scheduler = new YouTubeDownloadScheduler(
            NullLogger<YouTubeDownloadScheduler>.Instance,
            trackQueue,
            downloadQueue,
            Substitute.For<IDiscordFeedbackService>(),
            trackStorage
        );

        await scheduler.EnsureNextTrackQueuedAsync(GuildId, CancellationToken.None);

        await Assert
            .That(trackQueue.QueuedTracks(GuildId)[0].Status)
            .IsEqualTo(QueuedTrackStatus.Available);
        await downloadQueue
            .DidNotReceive()
            .QueueAsync(Arg.Any<Func<CancellationToken, YouTubeDownloadRequest>>());
    }
}
