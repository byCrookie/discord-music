using DiscordMusic.Core.Queues;
using DiscordMusic.Core.YouTube.Searching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Client.YouTube;

public class YouTubeSearchRequestConsumerService(
    IBackgroundQueue<YouTubeSearchRequest> searchQueue,
    ILogger<YouTubeSearchRequestConsumerService> logger,
    IYouTubeSearchRequestProcessor processor
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("YouTube search request queue consumer service is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var item = await searchQueue.DequeueAsync(stoppingToken);

            try
            {
                var request = item(stoppingToken);
                logger.LogInformation("Processing YouTube search request: {Request}...", request);
                await processor.ProcessAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred executing YouTube search request.");
            }
        }
    }
}
