using DiscordMusic.Core.Queues;
using DiscordMusic.Core.YouTube.Downloading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Client.YouTube;

public class YouTubeDownloadRequestConsumerService(
    IBackgroundQueue<YouTubeDownloadRequest> queue,
    ILogger<YouTubeDownloadRequestConsumerService> logger,
    IYouTubeDownloadRequestProcessor processor
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("YouTube download queue consumer is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var item = await queue.DequeueAsync(stoppingToken);

            try
            {
                var request = item(stoppingToken);
                logger.LogDebug(
                    "Dequeued YouTube download request. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                    request.Origin.GuildId,
                    request.Track.Id,
                    request.Track.Name
                );
                await processor.ProcessAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "YouTube download request processing failed.");
            }
        }
    }
}
