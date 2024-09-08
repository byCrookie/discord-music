using DiscordMusic.Cli.Discord.Music.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Music;

internal class MusicHost(
    IMusicStreamer streamer,
    ILogger<MusicHost> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            logger.LogTrace("Starting music host");

            var delay = 1;
            const int maxDelay = 10;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (streamer.CanExecute())
                {
                    logger.LogTrace("Executing music streamer");
                    await streamer.ExecuteAsync(stoppingToken);
                    delay = 1;
                    continue;
                }

                logger.LogTrace("Waiting {Delay}s for music streamer", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
                delay = Math.Min(delay + 1, maxDelay);
            }

            logger.LogTrace("Stopping music host");
        }, stoppingToken);
    }
}
