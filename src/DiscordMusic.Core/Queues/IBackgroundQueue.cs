namespace DiscordMusic.Core.Queues;

public interface IBackgroundQueue<T>
{
    ValueTask<bool> QueueAsync(Func<CancellationToken, T> item);
    ValueTask<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken);
}
