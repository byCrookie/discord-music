namespace DiscordMusic.Core.Queues;

internal sealed class GuildTrackQueue
{
    private readonly Lock _lock = new();
    private readonly LinkedList<QueuedTrack> _items = [];
    private TaskCompletionSource _changed = NewChangedSignal();

    public Task WaitForChangeAsync(CancellationToken cancellationToken)
    {
        Task changed;
        lock (_lock)
        {
            changed = _changed.Task;
        }

        return changed.WaitAsync(cancellationToken);
    }

    public void Clear()
    {
        TaskCompletionSource changed;
        lock (_lock)
        {
            _items.Clear();
            changed = MarkChanged();
        }

        changed.TrySetResult();
    }

    public void ClearFailedOnly()
    {
        TaskCompletionSource? changed = null;
        lock (_lock)
        {
            var failedTracks = _items
                .Where(trackQueueItem => trackQueueItem.Status == QueuedTrackStatus.Failed)
                .ToList();

            foreach (var trackQueueItem in failedTracks)
            {
                _items.Remove(trackQueueItem);
                changed ??= MarkChanged();
            }
        }

        changed?.TrySetResult();
    }

    public void SkipTo(int index)
    {
        TaskCompletionSource? changed = null;
        lock (_lock)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }

            for (var i = 0; i < index; i++)
            {
                _items.RemoveFirst();
                changed ??= MarkChanged();
            }
        }

        changed?.TrySetResult();
    }

    public int Count()
    {
        lock (_lock)
        {
            return _items.Count;
        }
    }

    public void Shuffle()
    {
        TaskCompletionSource changed;
        lock (_lock)
        {
            var items = _items.ToArray();
            Random.Shared.Shuffle(items.AsSpan());
            _items.Clear();
            foreach (var item in items)
            {
                _items.AddLast(item);
            }

            changed = MarkChanged();
        }

        changed.TrySetResult();
    }

    public bool TryUpdateStatus(string id, QueuedTrackStatus status)
    {
        TaskCompletionSource? changed = null;
        var updated = false;
        lock (_lock)
        {
            var node = _items.First;
            while (node is not null)
            {
                if (node.Value.Track.Id == id)
                {
                    node.Value = node.Value with { Status = status };
                    changed = MarkChanged();
                    updated = true;
                    break;
                }

                node = node.Next;
            }
        }

        changed?.TrySetResult();
        return updated;
    }

    public void EnqueueLast(QueuedTrack item)
    {
        TaskCompletionSource changed;
        lock (_lock)
        {
            _items.AddLast(item);
            changed = MarkChanged();
        }

        changed.TrySetResult();
    }

    public void EnqueueFirst(QueuedTrack item)
    {
        TaskCompletionSource changed;
        lock (_lock)
        {
            _items.AddFirst(item);
            changed = MarkChanged();
        }

        changed.TrySetResult();
    }

    public bool TryDequeueFirstAvailable(out QueuedTrack? item)
    {
        item = null;
        TaskCompletionSource? changed = null;
        var dequeued = false;
        lock (_lock)
        {
            foreach (
                var trackQueueItem in _items.Where(trackQueueItem =>
                    trackQueueItem.Status == QueuedTrackStatus.Available
                )
            )
            {
                item = trackQueueItem;
                _items.Remove(trackQueueItem);
                changed = MarkChanged();
                dequeued = true;
                break;
            }
        }

        changed?.TrySetResult();
        return dequeued;
    }

    public bool TryDequeueFirstAvailableInOrder(out QueuedTrack? item)
    {
        item = null;
        TaskCompletionSource? changed = null;
        var dequeued = false;
        lock (_lock)
        {
            var node = _items.First;
            while (node is not null)
            {
                if (node.Value.Status == QueuedTrackStatus.Failed)
                {
                    node = node.Next;
                    continue;
                }

                if (node.Value.Status != QueuedTrackStatus.Available)
                {
                    return false;
                }

                item = node.Value;
                _items.Remove(node);
                changed = MarkChanged();
                dequeued = true;
                break;
            }
        }

        changed?.TrySetResult();
        return dequeued;
    }

    public bool TryPeekFirstNonFailed(out QueuedTrack? item)
    {
        lock (_lock)
        {
            foreach (
                var trackQueueItem in _items.Where(trackQueueItem =>
                    trackQueueItem.Status != QueuedTrackStatus.Failed
                )
            )
            {
                item = trackQueueItem;
                return true;
            }
        }

        item = null;
        return false;
    }

    public bool TryMarkNextPendingAsDownloading(out QueuedTrack? item)
    {
        item = null;
        TaskCompletionSource? changed = null;
        var marked = false;
        lock (_lock)
        {
            var node = _items.First;
            while (node is not null)
            {
                if (node.Value.Status == QueuedTrackStatus.Failed)
                {
                    node = node.Next;
                    continue;
                }

                if (node.Value.Status != QueuedTrackStatus.Pending)
                {
                    return false;
                }

                item = node.Value with { Status = QueuedTrackStatus.Downloading };
                node.Value = item.Value;
                changed = MarkChanged();
                marked = true;
                break;
            }
        }

        changed?.TrySetResult();
        return marked;
    }

    public bool TryRemoveFirstNonFailed(out QueuedTrack? item)
    {
        item = null;
        TaskCompletionSource? changed = null;
        var removed = false;
        lock (_lock)
        {
            var node = _items.First;
            while (node is not null)
            {
                if (node.Value.Status == QueuedTrackStatus.Failed)
                {
                    node = node.Next;
                    continue;
                }

                item = node.Value;
                _items.Remove(node);
                changed = MarkChanged();
                removed = true;
                break;
            }
        }

        changed?.TrySetResult();
        return removed;
    }

    public IReadOnlyList<QueuedTrack> QueuedTracks()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    private TaskCompletionSource MarkChanged()
    {
        var changed = _changed;
        _changed = NewChangedSignal();
        return changed;
    }

    private static TaskCompletionSource NewChangedSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
