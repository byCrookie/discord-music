using System.Diagnostics;
using DiscordMusic.Client.YouTube;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.YouTube.Downloading;
using Flurl;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordMusic.Core.Tests.YouTube.Downloading;

[NotInParallel("ActivitySource")]
public class YouTubeDownloadRequestConsumerServiceTests
{
    [Test]
    public async Task ConsumeActivityIsOkWhenProcessingCompletes()
    {
        using var activityCapture = new ActivityCapture("youtube.download.consume");
        var service = CreateService(
            new SingleItemQueue(_ => CreateRequest()),
            new DelegateDownloadRequestProcessor((_, _) => Task.CompletedTask)
        );

        await service.StartAsync(CancellationToken.None);
        var activity = await activityCapture.WaitAsync();
        await service.StopAsync(CancellationToken.None);

        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(activity.StatusDescription).IsNull();
        await Assert.That(activity.GetTagItem("result")).IsEqualTo("completed");
    }

    [Test]
    public async Task ConsumeActivityIsErrorWhenItemFactoryFails()
    {
        using var activityCapture = new ActivityCapture("youtube.download.consume");
        var service = CreateService(
            new SingleItemQueue(_ => throw new InvalidOperationException("item failed")),
            new DelegateDownloadRequestProcessor((_, _) => Task.CompletedTask)
        );

        await service.StartAsync(CancellationToken.None);
        var activity = await activityCapture.WaitAsync();
        await service.StopAsync(CancellationToken.None);

        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("item failed");
        await Assert.That(activity.GetTagItem("result")).IsEqualTo("failed");
    }

    [Test]
    public async Task ConsumeActivityIsErrorWhenProcessorFails()
    {
        using var activityCapture = new ActivityCapture("youtube.download.consume");
        var service = CreateService(
            new SingleItemQueue(_ => CreateRequest()),
            new DelegateDownloadRequestProcessor(
                (_, _) => throw new InvalidOperationException("processor failed")
            )
        );

        await service.StartAsync(CancellationToken.None);
        var activity = await activityCapture.WaitAsync();
        await service.StopAsync(CancellationToken.None);

        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("processor failed");
        await Assert.That(activity.GetTagItem("result")).IsEqualTo("failed");
    }

    [Test]
    public async Task ConsumeActivityIsErrorWhenProcessorCancelsWithoutShutdown()
    {
        using var activityCapture = new ActivityCapture("youtube.download.consume");
        var service = CreateService(
            new SingleItemQueue(_ => CreateRequest()),
            new DelegateDownloadRequestProcessor(
                (_, _) => throw new OperationCanceledException("processor canceled")
            )
        );

        await service.StartAsync(CancellationToken.None);
        var activity = await activityCapture.WaitAsync();
        await service.StopAsync(CancellationToken.None);

        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("processor canceled");
        await Assert.That(activity.GetTagItem("result")).IsEqualTo("failed");
    }

    [Test]
    public async Task ConsumeActivityIsOkStoppedWhenServiceStopsDuringProcessing()
    {
        using var activityCapture = new ActivityCapture("youtube.download.consume");
        var processorStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var service = CreateService(
            new SingleItemQueue(_ => CreateRequest()),
            new DelegateDownloadRequestProcessor(
                async (_, cancellationToken) =>
                {
                    processorStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                        .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                }
            )
        );

        await service.StartAsync(CancellationToken.None);
        await processorStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);
        var activity = await activityCapture.WaitAsync();

        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(activity.GetTagItem("result")).IsEqualTo("stopped");
    }

    private static YouTubeDownloadRequestConsumerService CreateService(
        IBackgroundQueue<YouTubeDownloadRequest> queue,
        IYouTubeDownloadRequestProcessor processor
    )
    {
        return new YouTubeDownloadRequestConsumerService(
            queue,
            NullLogger<YouTubeDownloadRequestConsumerService>.Instance,
            processor
        );
    }

    private static YouTubeDownloadRequest CreateRequest()
    {
        return new YouTubeDownloadRequest(
            new Track(
                "track-id",
                "Track Name",
                "Artist",
                new Url("https://www.youtube.com/watch?v=track"),
                TimeSpan.FromMinutes(3)
            ),
            new DiscordRequestOrigin(1, 2, 3)
        );
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly string _operationName;

        private readonly TaskCompletionSource<Activity> _activityStopped = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private readonly ActivityListener _listener;

        public ActivityCapture(string operationName)
        {
            _operationName = operationName;
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == DiscordMusicObservability.Name,
                Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = OnActivityStopped,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public async Task<Activity> WaitAsync()
        {
            return await _activityStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private void OnActivityStopped(Activity activity)
        {
            if (activity.OperationName == _operationName)
            {
                _activityStopped.TrySetResult(activity);
            }
        }
    }

    private sealed class SingleItemQueue(Func<CancellationToken, YouTubeDownloadRequest> item)
        : IBackgroundQueue<YouTubeDownloadRequest>
    {
        private int _dequeueCount;

        public ValueTask<bool> QueueAsync(Func<CancellationToken, YouTubeDownloadRequest> item)
        {
            throw new NotSupportedException();
        }

        public async ValueTask<Func<CancellationToken, YouTubeDownloadRequest>> DequeueAsync(
            CancellationToken cancellationToken
        )
        {
            if (Interlocked.Increment(ref _dequeueCount) == 1)
            {
                return item;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class DelegateDownloadRequestProcessor(
        Func<YouTubeDownloadRequest, CancellationToken, Task> process
    ) : IYouTubeDownloadRequestProcessor
    {
        public Task ProcessAsync(
            YouTubeDownloadRequest request,
            CancellationToken cancellationToken
        )
        {
            return process(request, cancellationToken);
        }
    }
}
