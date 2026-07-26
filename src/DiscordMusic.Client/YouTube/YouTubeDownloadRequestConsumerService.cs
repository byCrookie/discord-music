using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordMusic.Core.Observability;
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
    private static readonly Counter<long> DownloadRequestsConsumed =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.youtube.download.queue.consumed",
            unit: "1",
            description: "YouTube download queue items consumed by the worker."
        );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("YouTube download queue consumer is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<CancellationToken, YouTubeDownloadRequest> item;

            try
            {
                item = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            ulong? guildId = null;
            var result = "completed";
            using var activity = DiscordMusicObservability.StartActivity(
                "youtube.download.consume"
            );
            try
            {
                var request = item(stoppingToken);
                guildId = request.Origin.GuildId;
                DiscordMusicObservability.SetGuildTag(activity, request.Origin.GuildId);
                DiscordMusicObservability.SetTag(activity, "music.track.id", request.Track.Id);
                DiscordMusicObservability.SetTag(
                    activity,
                    "discord.user.id",
                    request.Origin.UserId.ToString()
                );
                logger.LogDebug(
                    "Dequeued YouTube download request. GuildId={GuildId}, TrackId={TrackId}, Title={Title}",
                    request.Origin.GuildId,
                    request.Track.Id,
                    request.Track.Name
                );
                await processor.ProcessAsync(request, stoppingToken);
                activity?.SetStatus(ActivityStatusCode.Ok);
                Record(request.Origin.GuildId, "completed");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                result = "stopped";
                activity?.SetStatus(ActivityStatusCode.Ok, result);
                break;
            }
            catch (Exception ex)
            {
                result = "failed";
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                Record(guildId, "failed");
                logger.LogError(ex, "YouTube download request processing failed.");
            }
            finally
            {
                DiscordMusicObservability.SetTag(activity, "result", result);
            }
        }
    }

    private static void Record(ulong? guildId, string result)
    {
        if (guildId is { } id)
        {
            Record(id, result);
            return;
        }

        DownloadRequestsConsumed.Add(1, new KeyValuePair<string, object?>("result", result));
    }

    private static void Record(ulong guildId, string result)
    {
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("result", result);
        DownloadRequestsConsumed.Add(1, tags);
    }
}
