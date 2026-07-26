using System.Diagnostics.Metrics;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Queues;

internal class TrackQueue : ITrackQueue
{
    private readonly Lock _lock = new();
    private readonly Dictionary<ulong, GuildTrackQueue> _queues = [];
    private readonly ILogger<TrackQueue> _logger;

    public TrackQueue(ILogger<TrackQueue> logger)
    {
        _logger = logger;
        DiscordMusicObservability.Meter.CreateObservableGauge(
            "discord.music.queue.tracks.current",
            ObserveCurrentTracks,
            unit: "{track}",
            description: "Current queued tracks by guild and status."
        );
    }

    public Task WaitForChangeAsync(ulong guildId, CancellationToken cancellationToken)
    {
        return Queue(guildId).WaitForChangeAsync(cancellationToken);
    }

    public void Clear(ulong guildId)
    {
        _logger.LogTrace("Clear queue for guild {GuildId}", guildId);
        Queue(guildId).Clear();
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("operation", "clear");
        DiscordMusicObservability.QueueMutations.Add(1, tags);
    }

    public void ClearFailedOnly(ulong guildId)
    {
        _logger.LogTrace("Clear failed items from queue for guild {GuildId}", guildId);
        Queue(guildId).ClearFailedOnly();
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("operation", "clear_failed");
        DiscordMusicObservability.QueueMutations.Add(1, tags);
    }

    public void SkipTo(ulong guildId, int index)
    {
        _logger.LogTrace("Skip to item at index {Index} for guild {GuildId}", index, guildId);
        Queue(guildId).SkipTo(index);
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("operation", "skip_to");
        DiscordMusicObservability.QueueMutations.Add(1, tags);
    }

    public int Count(ulong guildId)
    {
        var count = Queue(guildId).Count();
        _logger.LogTrace("Queue count is {Count} for guild {GuildId}", count, guildId);
        return count;
    }

    public void Shuffle(ulong guildId)
    {
        _logger.LogTrace("Shuffle queue for guild {GuildId}", guildId);
        Queue(guildId).Shuffle();
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("operation", "shuffle");
        DiscordMusicObservability.QueueMutations.Add(1, tags);
    }

    public bool TryUpdateStatus(ulong guildId, string id, QueuedTrackStatus status)
    {
        var updated = Queue(guildId).TryUpdateStatus(id, status);
        if (!updated)
        {
            _logger.LogTrace(
                "No item found with id {Id} in guild {GuildId} to update",
                id,
                guildId
            );
        }

        return updated;
    }

    public void EnqueueLast(ulong guildId, QueuedTrack item)
    {
        _logger.LogTrace("Enqueue item {Item} for guild {GuildId}", item, guildId);
        Queue(guildId).EnqueueLast(item);
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("placement", "last");
        tags.Add("music.queue.status", item.Status.ToString());
        DiscordMusicObservability.TracksQueued.Add(1, tags);
    }

    public void EnqueueFirst(ulong guildId, QueuedTrack item)
    {
        _logger.LogTrace("Enqueue next item {Item} for guild {GuildId}", item, guildId);
        Queue(guildId).EnqueueFirst(item);
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("placement", "first");
        tags.Add("music.queue.status", item.Status.ToString());
        DiscordMusicObservability.TracksQueued.Add(1, tags);
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
            _logger.LogInformation(
                "Marked queued track {TrackId} as downloading in guild {GuildId}.",
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
            _logger.LogInformation(
                "Removed queued track {TrackId} from guild {GuildId}.",
                queuedTrack.Track.Id,
                guildId
            );
            var tags = DiscordMusicObservability.GuildTags(guildId);
            tags.Add("operation", "remove_first_non_failed");
            DiscordMusicObservability.QueueMutations.Add(1, tags);
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

    private IEnumerable<Measurement<int>> ObserveCurrentTracks()
    {
        KeyValuePair<ulong, GuildTrackQueue>[] queues;
        lock (_lock)
        {
            queues = _queues.ToArray();
        }

        foreach (var (guildId, queue) in queues)
        {
            foreach (var (status, count) in queue.CountByStatus())
            {
                yield return new Measurement<int>(
                    count,
                    new KeyValuePair<string, object?>("discord.guild.id", guildId.ToString()),
                    new KeyValuePair<string, object?>("music.queue.status", status.ToString())
                );
            }
        }
    }
}
