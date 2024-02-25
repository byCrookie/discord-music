using System.Reactive.Concurrency;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Music.Queue;

internal class MusicQueue(ILogger<MusicQueue> logger) : IMusicQueue
{
    private readonly object _lock = new AsyncLock();
    private readonly LinkedList<Track> _queue = [];

    public void Enqueue(Track track)
    {
        lock (_lock)
        {
            logger.LogTrace("Enqueue track {Track}.", track);
            _queue.AddLast(track);
        }
    }

    public void EnqueueNext(Track track)
    {
        lock (_lock)
        {
            logger.LogTrace("Enqueue next track {Track}.", track);
            _queue.AddFirst(track);
        }
    }

    public void EnqueueNextWithDequeue(Track track)
    {
        lock (_lock)
        {
            _ = TryDequeue(out _);
            logger.LogTrace("Enqueue next track {Track}.", track);
            _queue.AddFirst(track);
        }
    }

    public bool TryDequeue(out Track? track)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                logger.LogTrace("No tracks to dequeue.");
                track = null;
                return false;
            }

            track = _queue.First?.Value;
            logger.LogTrace("Dequeue track {Track}.", track);
            _queue.RemoveFirst();
            return true;
        }
    }

    public bool TryPeek(out Track? track)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                logger.LogTrace("No tracks to peek.");
                track = null;
                return false;
            }

            track = _queue.First?.Value;
            logger.LogTrace("Peek track {Track}.", track);
            return true;
        }
    }

    public IEnumerable<Track> GetAll()
    {
        lock (_lock)
        {
            logger.LogTrace("Get all tracks.");
            return _queue;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            logger.LogTrace("Clear queue.");
            _queue.Clear();
        }
    }

    public void SkipTo(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _queue.Count)
            {
                logger.LogTrace("Invalid index {Index} to skip to.", index);
                return;
            }

            logger.LogTrace("Skip to track at index {Index}.", index);
            for (var i = 0; i < index; i++)
            {
                logger.LogTrace("Skip track {Track}.", _queue.First?.Value);
                _queue.RemoveFirst();
            }
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            logger.LogTrace("Queue count is {Count}.", _queue.Count);
            return _queue.Count;
        }
    }
}
