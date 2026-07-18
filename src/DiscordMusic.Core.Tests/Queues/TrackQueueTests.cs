using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Tracks;
using Flurl;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordMusic.Core.Tests.Queue;

public class TrackQueueTests
{
    private const ulong GuildId = 1;
    private const ulong OtherGuildId = 2;

    [Test]
    public async Task TryDequeueFirstAvailableInOrderWaitsForEarlierPendingTrack()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var pendingTrack = CreateTrack("pending");
        var availableTrack = CreateTrack("available");

        queue.EnqueueLast(GuildId, new QueuedTrack(pendingTrack, QueuedTrackStatus.Pending));
        queue.EnqueueLast(GuildId, new QueuedTrack(availableTrack, QueuedTrackStatus.Available));

        var dequeued = queue.TryDequeueFirstAvailableInOrder(GuildId, out var item);

        await Assert.That(dequeued).IsFalse();
        await Assert.That(item).IsNull();
        await Assert.That(queue.Count(GuildId)).IsEqualTo(2);
    }

    [Test]
    public async Task TryDequeueFirstAvailableInOrderSkipsFailedTracks()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var failedTrack = CreateTrack("failed");
        var availableTrack = CreateTrack("available");

        queue.EnqueueLast(GuildId, new QueuedTrack(failedTrack, QueuedTrackStatus.Failed));
        queue.EnqueueLast(GuildId, new QueuedTrack(availableTrack, QueuedTrackStatus.Available));

        var dequeued = queue.TryDequeueFirstAvailableInOrder(GuildId, out var item);

        await Assert.That(dequeued).IsTrue();
        await Assert.That(item?.Track.Id).IsEqualTo(availableTrack.Id);
        await Assert.That(queue.Count(GuildId)).IsEqualTo(1);
    }

    [Test]
    public async Task WaitForChangeCompletesWhenStatusChanges()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var track = CreateTrack("track");
        queue.EnqueueLast(GuildId, new QueuedTrack(track, QueuedTrackStatus.Pending));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var wait = queue.WaitForChangeAsync(GuildId, cancellation.Token);

        queue.TryUpdateStatus(GuildId, track.Id, QueuedTrackStatus.Available);

        await wait;
        await Assert.That(wait.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task QueueStateIsIsolatedByGuild()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var track = CreateTrack("track");

        queue.EnqueueLast(GuildId, new QueuedTrack(track, QueuedTrackStatus.Available));

        await Assert.That(queue.Count(GuildId)).IsEqualTo(1);
        await Assert.That(queue.Count(OtherGuildId)).IsEqualTo(0);
    }

    [Test]
    public async Task TryMarkNextPendingAsDownloadingMarksOnlyFirstNonFailedPendingTrack()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var failedTrack = CreateTrack("failed");
        var firstPendingTrack = CreateTrack("first-pending");
        var secondPendingTrack = CreateTrack("second-pending");

        queue.EnqueueLast(GuildId, new QueuedTrack(failedTrack, QueuedTrackStatus.Failed));
        queue.EnqueueLast(GuildId, new QueuedTrack(firstPendingTrack, QueuedTrackStatus.Pending));
        queue.EnqueueLast(GuildId, new QueuedTrack(secondPendingTrack, QueuedTrackStatus.Pending));

        var marked = queue.TryMarkNextPendingAsDownloading(GuildId, out var item);
        var tracks = queue.QueuedTracks(GuildId);

        await Assert.That(marked).IsTrue();
        await Assert.That(item?.Track.Id).IsEqualTo(firstPendingTrack.Id);
        await Assert.That(tracks[0].Status).IsEqualTo(QueuedTrackStatus.Failed);
        await Assert.That(tracks[1].Status).IsEqualTo(QueuedTrackStatus.Downloading);
        await Assert.That(tracks[2].Status).IsEqualTo(QueuedTrackStatus.Pending);
    }

    [Test]
    public async Task TryMarkNextPendingAsDownloadingDoesNotLookPastAvailableTrack()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var availableTrack = CreateTrack("available");
        var pendingTrack = CreateTrack("pending");

        queue.EnqueueLast(GuildId, new QueuedTrack(availableTrack, QueuedTrackStatus.Available));
        queue.EnqueueLast(GuildId, new QueuedTrack(pendingTrack, QueuedTrackStatus.Pending));

        var marked = queue.TryMarkNextPendingAsDownloading(GuildId, out var item);

        await Assert.That(marked).IsFalse();
        await Assert.That(item).IsNull();
        await Assert
            .That(queue.QueuedTracks(GuildId)[1].Status)
            .IsEqualTo(QueuedTrackStatus.Pending);
    }

    [Test]
    public async Task TryRemoveFirstNonFailedSkipsFailedTracks()
    {
        var queue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var failedTrack = CreateTrack("failed");
        var pendingTrack = CreateTrack("pending");

        queue.EnqueueLast(GuildId, new QueuedTrack(failedTrack, QueuedTrackStatus.Failed));
        queue.EnqueueLast(GuildId, new QueuedTrack(pendingTrack, QueuedTrackStatus.Pending));

        var removed = queue.TryRemoveFirstNonFailed(GuildId, out var item);

        await Assert.That(removed).IsTrue();
        await Assert.That(item?.Track.Id).IsEqualTo(pendingTrack.Id);
        await Assert.That(queue.Count(GuildId)).IsEqualTo(1);
        await Assert.That(queue.QueuedTracks(GuildId)[0].Track.Id).IsEqualTo(failedTrack.Id);
    }

    private static Track CreateTrack(string id)
    {
        return new Track(
            id,
            $"Track {id}",
            "Artist",
            new Url($"https://example.com/{id}"),
            TimeSpan.FromMinutes(3)
        );
    }
}
