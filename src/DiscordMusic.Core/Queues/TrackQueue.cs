using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Queues;

internal class TrackQueue(ILogger<TrackQueue> logger) : ITrackQueue
{
    private readonly Lock _lock = new();
    private readonly Dictionary<ulong, GuildTrackQueue> _queues = [];

    public Task WaitForChangeAsync(ulong guildId, CancellationToken cancellationToken)
    {
        return Queue(guildId).WaitForChangeAsync(cancellationToken);
    }

    public void Clear(ulong guildId)
    {
        logger.LogTrace("Clear queue for guild {GuildId}", guildId);
        Queue(guildId).Clear();
    }

    public void ClearFailedOnly(ulong guildId)
    {
        logger.LogTrace("Clear failed items from queue for guild {GuildId}", guildId);
        Queue(guildId).ClearFailedOnly();
    }

    public void SkipTo(ulong guildId, int index)
    {
        logger.LogTrace("Skip to item at index {Index} for guild {GuildId}", index, guildId);
        Queue(guildId).SkipTo(index);
    }

    public int Count(ulong guildId)
    {
        var count = Queue(guildId).Count();
        logger.LogTrace("Queue count is {Count} for guild {GuildId}", count, guildId);
        return count;
    }

    public void Shuffle(ulong guildId)
    {
        logger.LogTrace("Shuffle queue for guild {GuildId}", guildId);
        Queue(guildId).Shuffle();
    }

    public bool TryUpdateStatus(ulong guildId, string id, QueuedTrackStatus status)
    {
        var updated = Queue(guildId).TryUpdateStatus(id, status);
        if (!updated)
        {
            logger.LogTrace("No item found with id {Id} in guild {GuildId} to update", id, guildId);
        }

        return updated;
    }

    public void EnqueueLast(ulong guildId, QueuedTrack item)
    {
        logger.LogTrace("Enqueue item {Item} for guild {GuildId}", item, guildId);
        Queue(guildId).EnqueueLast(item);
    }

    public void EnqueueFirst(ulong guildId, QueuedTrack item)
    {
        logger.LogTrace("Enqueue next item {Item} for guild {GuildId}", item, guildId);
        Queue(guildId).EnqueueFirst(item);
    }

    public bool TryDequeueFirstAvailable(ulong guildId, out QueuedTrack? item)
    {
        return Queue(guildId).TryDequeueFirstAvailable(out item);
    }

    public bool TryDequeueFirstAvailableInOrder(ulong guildId, out QueuedTrack? item)
    {
        return Queue(guildId).TryDequeueFirstAvailableInOrder(out item);
    }

    public bool TryPeekFirstNonFailed(ulong guildId, out QueuedTrack? item)
    {
        return Queue(guildId).TryPeekFirstNonFailed(out item);
    }

    public bool TryMarkNextPendingAsDownloading(ulong guildId, out QueuedTrack? item)
    {
        var marked = Queue(guildId).TryMarkNextPendingAsDownloading(out item);
        if (marked && item is { } queuedTrack)
        {
            logger.LogInformation(
                "Queued track {TrackId} for lazy download in guild {GuildId}.",
                queuedTrack.Track.Id,
                guildId
            );
        }

        return marked;
    }

    public bool TryRemoveFirstNonFailed(ulong guildId, out QueuedTrack? item)
    {
        var removed = Queue(guildId).TryRemoveFirstNonFailed(out item);
        if (removed && item is { } queuedTrack)
        {
            logger.LogInformation(
                "Removed queued track {TrackId} from guild {GuildId}.",
                queuedTrack.Track.Id,
                guildId
            );
        }

        return removed;
    }

    public IReadOnlyList<QueuedTrack> QueuedTracks(ulong guildId)
    {
        return Queue(guildId).QueuedTracks();
    }

    private GuildTrackQueue Queue(ulong guildId)
    {
        lock (_lock)
        {
            if (!_queues.TryGetValue(guildId, out var queue))
            {
                queue = new GuildTrackQueue();
                _queues.Add(guildId, queue);
            }

            return queue;
        }
    }
}
