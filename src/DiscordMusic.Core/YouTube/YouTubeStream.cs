using System.Diagnostics;

namespace DiscordMusic.Core.YouTube;

public record YouTubeStream(Process YtDlpProcess, Process FfmpegProcess, Task Start, Stream Output) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await CastAndDispose(YtDlpProcess);
        await CastAndDispose(FfmpegProcess);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
            {
                await resourceAsyncDisposable.DisposeAsync();
            }
            else
            {
                resource.Dispose();
            }
        }
    }
}
