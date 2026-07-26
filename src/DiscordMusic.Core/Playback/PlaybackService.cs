using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordMusic.Core.Audio.Sending;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.YouTube.Downloading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Playback;

internal sealed class PlaybackService(
    ITrackQueue trackQueue,
    ITrackStorage trackStorage,
    IAudioSender audioSender,
    IYouTubeDownloadScheduler downloadScheduler,
    IDiscordFeedbackService feedback,
    ILogger<PlaybackService> logger,
    TimeProvider timeProvider
) : BackgroundService
{
    private readonly ConcurrentDictionary<ulong, PlaybackLoop> _playbackLoops = [];
    private static readonly Counter<long> PlaybackTracks =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.playback.tracks",
            unit: "1",
            description: "Playback track attempts."
        );
    private static readonly Histogram<double> PlaybackTrackDuration =
        DiscordMusicObservability.Meter.CreateHistogram<double>(
            "discord.music.playback.track.duration",
            unit: "s",
            description: "Playback track duration."
        );
    private static readonly Counter<long> VoiceJobContentions =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.discord.voice.job.contentions",
            unit: "1",
            description: "Failed voice job acquisitions because another job is active."
        );

    public void Start(ulong guildId, VoiceConnection voiceInstance)
    {
        using var activity = DiscordMusicObservability.StartActivity("playback.loop.start");
        DiscordMusicObservability.SetGuildTag(activity, guildId);
        RecordPlaybackLoop(guildId, "start", "requested");
        _playbackLoops.AddOrUpdate(
            guildId,
            _ => StartLoop(guildId, voiceInstance),
            (_, existing) =>
            {
                if (!existing.Task.IsCompleted)
                {
                    return existing;
                }

                existing.CancellationTokenSource.Dispose();
                return StartLoop(guildId, voiceInstance);
            }
        );
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    public bool TryGetPlaybackSession(ulong guildId, out PlaybackSession session)
    {
        if (_playbackLoops.TryGetValue(guildId, out var loop) && !loop.Task.IsCompleted)
        {
            session = loop.VoiceConnection.PlaybackSession;
            return true;
        }

        session = null!;
        return false;
    }

    public void Stop(ulong guildId)
    {
        if (_playbackLoops.TryRemove(guildId, out var loop))
        {
            using var activity = DiscordMusicObservability.StartActivity("playback.loop.stop");
            DiscordMusicObservability.SetGuildTag(activity, guildId);
            loop.CancellationTokenSource.Cancel();
            loop.CancellationTokenSource.Dispose();
            RecordPlaybackLoop(guildId, "stop", "stopped");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            RecordPlaybackLoop(guildId, "stop", "not_found");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            foreach (var guildId in _playbackLoops.Keys)
            {
                Stop(guildId);
            }
        }
    }

    private PlaybackLoop StartLoop(ulong guildId, VoiceConnection voiceInstance)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            voiceInstance.CancellationToken
        );
        var task = RunGuildPlaybackLoopAsync(guildId, voiceInstance, cancellation.Token);
        _ = ObserveLoopAsync(guildId, task);
        return new PlaybackLoop(task, cancellation, voiceInstance);
    }

    private async Task ObserveLoopAsync(ulong guildId, Task loop)
    {
        try
        {
            await loop;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Guild playback loop faulted. GuildId={GuildId}", guildId);
        }
    }

    private async Task RunGuildPlaybackLoopAsync(
        ulong guildId,
        VoiceConnection voiceInstance,
        CancellationToken cancellationToken
    )
    {
        using var activity = DiscordMusicObservability.StartActivity("playback.loop.run");
        DiscordMusicObservability.SetGuildTag(activity, guildId);
        RecordPlaybackLoop(guildId, "run", "started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var job = voiceInstance.TryEnterJob(VoiceJobType.Playing);
                    if (job is null)
                    {
                        var tags = DiscordMusicObservability.GuildTags(guildId);
                        tags.Add("voice.job.type", nameof(VoiceJobType.Playing));
                        VoiceJobContentions.Add(1, tags);
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(250),
                            timeProvider,
                            cancellationToken
                        );
                        continue;
                    }

                    await RunPlaybackJobAsync(guildId, voiceInstance, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    activity?.AddException(ex);
                    RecordPlaybackLoop(guildId, "run", "crashed");
                    logger.LogError(ex, "Guild playback loop crashed. GuildId={GuildId}", guildId);
                    await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, cancellationToken);
                }
            }

            activity?.SetStatus(ActivityStatusCode.Ok, "stopped");
            RecordPlaybackLoop(guildId, "run", "stopped");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            RecordPlaybackLoop(guildId, "run", "failed");
            throw;
        }
    }

    private sealed record PlaybackLoop(
        Task Task,
        CancellationTokenSource CancellationTokenSource,
        VoiceConnection VoiceConnection
    );

    private static void RecordPlaybackLoop(ulong guildId, string lifecycleEvent, string result)
    {
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("event", lifecycleEvent);
        tags.Add("result", result);
        DiscordMusicObservability.PlaybackLoops.Add(1, tags);
    }

    private async Task RunPlaybackJobAsync(
        ulong guildId,
        VoiceConnection voiceInstance,
        CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!trackQueue.TryDequeueFirstAvailableInOrder(guildId, out var queuedTrack))
            {
                await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
                await trackQueue.WaitForChangeAsync(guildId, cancellationToken);
                continue;
            }

            if (queuedTrack is not { } item)
            {
                continue;
            }

            await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
            await PlayTrackAsync(guildId, voiceInstance, item, cancellationToken);
        }
    }

    private async Task PlayTrackAsync(
        ulong guildId,
        VoiceConnection voiceInstance,
        QueuedTrack queuedTrack,
        CancellationToken cancellationToken
    )
    {
        var track = queuedTrack.Track;
        var startPosition = TimeSpan.Zero;

        while (!cancellationToken.IsCancellationRequested)
        {
            var startedAt = timeProvider.GetTimestamp();
            using var activity = DiscordMusicObservability.StartActivity("playback.track");
            DiscordMusicObservability.SetGuildTag(activity, guildId);
            DiscordMusicObservability.SetTag(activity, "music.track.id", track.Id);
            DiscordMusicObservability.SetTag(
                activity,
                "music.playback.start_position_ms",
                startPosition.TotalMilliseconds
            );

            using var trackLease = voiceInstance.PlaybackSession.BeginTrack(
                track,
                startPosition,
                cancellationToken
            );

            try
            {
                logger.LogInformation(
                    "Playing track {TrackId} in guild {GuildId} from {Position}.",
                    track.Id,
                    guildId,
                    startPosition
                );

                await audioSender.SendAsync(
                    guildId,
                    voiceInstance.Client,
                    track,
                    trackStorage.GetTrackPath(track, "pcm"),
                    startPosition,
                    voiceInstance.PlaybackSession,
                    trackLease.CancellationToken
                );
                activity?.SetStatus(ActivityStatusCode.Ok);
                var tags = DiscordMusicObservability.GuildTags(guildId);
                tags.Add("result", "completed");
                PlaybackTracks.Add(1, tags);
                PlaybackTrackDuration.Record(
                    timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    tags
                );
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var request = voiceInstance.PlaybackSession.ConsumeRequest();
                if (request.Type == PlaybackControlRequestType.Seek)
                {
                    activity?.SetStatus(ActivityStatusCode.Ok, "seek");
                    var tags = DiscordMusicObservability.GuildTags(guildId);
                    tags.Add("result", "seek");
                    PlaybackTrackDuration.Record(
                        timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                        tags
                    );
                    startPosition = request.Position;
                    continue;
                }

                activity?.SetStatus(ActivityStatusCode.Ok, request.Type.ToString());
                var controlTags = DiscordMusicObservability.GuildTags(guildId);
                controlTags.Add("result", request.Type.ToString());
                PlaybackTrackDuration.Record(
                    timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    controlTags
                );
                return;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "playback_failed");
                activity?.AddException(ex);
                var tags = DiscordMusicObservability.GuildTags(guildId);
                tags.Add("result", "failed");
                PlaybackTracks.Add(1, tags);
                PlaybackTrackDuration.Record(
                    timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                    tags
                );
                logger.LogError(
                    ex,
                    "Failed to play track {TrackId} in guild {GuildId}.",
                    track.Id,
                    guildId
                );
                if (queuedTrack.Origin is { } origin)
                {
                    await feedback.SendPlaybackFailureAsync(origin, track, cancellationToken);
                }
                return;
            }
        }
    }
}
