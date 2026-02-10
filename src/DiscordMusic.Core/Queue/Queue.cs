using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Queue;

internal class Queue<T>(ILogger<Queue<T>> logger) : IQueue<T>
    where T : class
{
    private readonly Lock _lock = new();
    private readonly LinkedList<T> _queue = [];

    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
            logger.LogTrace("Queue cleared");
        }
    }

    public void SkipTo(int index)
    {
        lock (_lock)
        {
            if ((uint)index >= (uint)_queue.Count)
            {
                logger.LogTrace(
                    "Invalid index {Index} to skip to (count={Count})",
                    index,
                    _queue.Count
                );
                return;
            }

            if (index == 0)
            {
                logger.LogTrace("SkipTo(0) no-op");
                return;
            }

            logger.LogTrace("Skip to item at index {Index}", index);
            for (var i = 0; i < index && _queue.First is not null; i++)
            {
                _queue.RemoveFirst();
            }
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            return _queue.Count;
        }
    }

    public void Shuffle()
    {
        lock (_lock)
        {
            if (_queue.Count <= 1)
            {
                logger.LogTrace("Shuffle no-op (count={Count})", _queue.Count);
                return;
            }

            var items = _queue.ToArray();
            Random.Shared.Shuffle(items.AsSpan());

            _queue.Clear();
            foreach (var item in items)
            {
                _queue.AddLast(item);
            }

            logger.LogTrace("Queue shuffled");
        }
    }

    public void EnqueueLast(T item)
    {
        lock (_lock)
        {
            _queue.AddLast(item);
            logger.LogTrace("Enqueued item (last)");
        }
    }

    public void EnqueueFirst(T item)
    {
        lock (_lock)
        {
            _queue.AddFirst(item);
            logger.LogTrace("Enqueued item (first)");
        }
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        lock (_lock)
        {
            var first = _queue.First;
            if (first is null)
            {
                item = null;
                return false;
            }

            item = first.Value;
            _queue.RemoveFirst();
            logger.LogTrace("Dequeued item");
            return true;
        }
    }

    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        lock (_lock)
        {
            var first = _queue.First;
            if (first is null)
            {
                item = null;
                return false;
            }

            item = first.Value;
            return true;
        }
    }

    public IReadOnlyList<T> Items()
    {
        lock (_lock)
        {
            // Snapshot to a list so callers can enumerate without holding our lock.
            return _queue.ToList();
        }
    }
}
