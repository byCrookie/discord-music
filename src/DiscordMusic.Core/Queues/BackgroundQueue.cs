using System.Threading.Channels;

namespace DiscordMusic.Core.Queues;

public class BackgroundQueue<T>(BoundedChannelOptions options) : IBackgroundQueue<T>
{
    private readonly Channel<Func<CancellationToken, T>> _queue = Channel.CreateBounded<
        Func<CancellationToken, T>
    >(options);

    public async ValueTask<bool> QueueAsync(Func<CancellationToken, T> item)
    {
        if (!await _queue.Writer.WaitToWriteAsync())
        {
            return false;
        }

        return _queue.Writer.TryWrite(item);
    }

    public ValueTask<Func<CancellationToken, T>> DequeueAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
