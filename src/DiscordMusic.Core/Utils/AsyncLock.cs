namespace DiscordMusic.Core.Utils;

public class AsyncLock
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public async Task<Lock> AquireAsync(CancellationToken ct)
    {
        await _semaphoreSlim.WaitAsync(ct);
        return new Lock(_semaphoreSlim);
    }

    public class Lock(SemaphoreSlim semaphoreSlim) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            semaphoreSlim.Release();
            return ValueTask.CompletedTask;
        }
    }
}
