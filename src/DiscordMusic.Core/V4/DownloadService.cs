using System.Threading.Channels;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.V4;

internal class DownloadService(
    ILogger<DownloadService> logger,
    Queue queue,
    DiskCache<MusicTrack> diskCache,
    YouTubeDownload youTubeDownload,
    MessageService messageService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var track in queue.DownloadRequests.ReadAllAsync(stoppingToken))
        {
            var query = $"{track.Name} {track.Artists}";

            var diskFile = await diskCache.GetDataPathAsync(track.Url, stoppingToken);

            if (diskFile.IsError)
            {
                logger.LogError("Failed to get disk file for url {Url}: {Error}",
                    track.Url, diskFile.FirstError);
                await messageService.Messages.WriteAsync();
                queue.NotifyTrackFault(track);
                continue;
            }

            var download = await youTubeDownload.DownloadAsync(query,
                diskFile.Value, stoppingToken);

            if (download.IsError)
            {
                queue.NotifyTrackFault(track);
                continue;
            }

            logger.LogInformation("Downloaded track {Query} to {DiskFile}", query,
                diskFile.Value);
        }
    }
}
