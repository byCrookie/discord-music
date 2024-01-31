using DiscordMusic.Core.Discord.Music.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Music;

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
            while (!stoppingToken.IsCancellationRequested)
            {
                if (streamer.CanExecute())
                {
                    logger.LogTrace("Executing music streamer");
                    await streamer.ExecuteAsync(stoppingToken);
                    continue;
                }

                logger.LogTrace("Waiting for music streamer");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            logger.LogTrace("Stopping music host");
        }, stoppingToken);
    }
}