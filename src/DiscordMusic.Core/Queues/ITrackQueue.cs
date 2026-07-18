namespace DiscordMusic.Core.Queues;

public interface ITrackQueue
{
    void EnqueueLast(ulong guildId, QueuedTrack item);
    void EnqueueFirst(ulong guildId, QueuedTrack item);
    bool TryDequeueFirstAvailable(ulong guildId, out QueuedTrack? item);
    bool TryDequeueFirstAvailableInOrder(ulong guildId, out QueuedTrack? item);
    bool TryPeekFirstNonFailed(ulong guildId, out QueuedTrack? item);
    bool TryMarkNextPendingAsDownloading(ulong guildId, out QueuedTrack? item);
    bool TryRemoveFirstNonFailed(ulong guildId, out QueuedTrack? item);
    IReadOnlyList<QueuedTrack> QueuedTracks(ulong guildId);
    Task WaitForChangeAsync(ulong guildId, CancellationToken cancellationToken);
    void Clear(ulong guildId);
    void ClearFailedOnly(ulong guildId);
    void SkipTo(ulong guildId, int index);
    int Count(ulong guildId);
    void Shuffle(ulong guildId);
    bool TryUpdateStatus(ulong guildId, string id, QueuedTrackStatus status);
}
