using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordMusic.Core.Observability;
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
    private static readonly Counter<long> SearchRequestsConsumed =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.youtube.search.queue.consumed",
            unit: "1",
            description: "YouTube search queue items consumed by the worker."
        );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("YouTube search request queue consumer service is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<CancellationToken, YouTubeSearchRequest> item;

            try
            {
                item = await searchQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            ulong? guildId = null;
            var result = "completed";
            using var activity = DiscordMusicObservability.StartActivity("youtube.search.consume");
            try
            {
                var request = item(stoppingToken);
                guildId = request.Origin.GuildId;
                DiscordMusicObservability.SetGuildTag(activity, request.Origin.GuildId);
                DiscordMusicObservability.SetTag(
                    activity,
                    "discord.user.id",
                    request.Origin.UserId.ToString()
                );
                DiscordMusicObservability.SetTag(
                    activity,
                    "music.queue.placement",
                    request.Placement.ToString()
                );
                logger.LogInformation("Processing YouTube search request: {Request}...", request);
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
                logger.LogError(ex, "Error occurred executing YouTube search request.");
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

        SearchRequestsConsumed.Add(1, new KeyValuePair<string, object?>("result", result));
    }

    private static void Record(ulong guildId, string result)
    {
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("result", result);
        SearchRequestsConsumed.Add(1, tags);
    }
}
