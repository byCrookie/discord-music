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
            logger.LogTrace("Clear queue");
            _queue.Clear();
        }
    }

    public void SkipTo(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _queue.Count)
            {
                logger.LogTrace("Invalid index {Index} to skip to", index);
                return;
            }

            logger.LogTrace("Skip to item at index {Index}", index);
            for (var i = 0; i < index; i++)
            {
                logger.LogTrace("Skip item {Item}", _queue.First?.Value);
                _queue.RemoveFirst();
            }
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            logger.LogTrace("Queue count is {Count}", _queue.Count);
            return _queue.Count;
        }
    }

    public void Shuffle()
    {
        lock (_lock)
        {
            logger.LogTrace("Shuffle queue");
            var items = _queue.ToArray();
            Random.Shared.Shuffle(items.AsSpan());
            _queue.Clear();
            foreach (var item in items)
            {
                _queue.AddLast(item);
            }
        }
    }

    public void EnqueueLast(T item)
    {
        lock (_lock)
        {
            logger.LogTrace("Enqueue item {Item}", item);
            _queue.AddLast(item);
        }
    }

    public void EnqueueFirst(T item)
    {
        lock (_lock)
        {
            logger.LogTrace("Enqueue next item {Item}", item);
            _queue.AddFirst(item);
        }
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                logger.LogTrace("No items to dequeue");
                item = null;
                return false;
            }

            item = _queue.First!.Value;
            logger.LogTrace("Dequeue item {Item}", item);
            _queue.RemoveFirst();
            return true;
        }
    }

    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                logger.LogTrace("No items to peek");
                item = null;
                return false;
            }

            item = _queue.First!.Value;
            logger.LogTrace("Peek item {Item}", item);
            return true;
        }
    }

    public IReadOnlyList<T> Items()
    {
        lock (_lock)
        {
            logger.LogTrace("Get all items");
            return _queue.ToList();
        }
    }
}
