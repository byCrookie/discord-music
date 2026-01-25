using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Rewrite.Workers;

public class DownloadWorker(DownloadQueue downloadQueue, IYouTubeDownload youtubeDownload) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var metadata in downloadQueue.Reader.ReadAllAsync(stoppingToken))
        {
        }
    }
}
