using System.Diagnostics.Metrics;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Downloading;

namespace DiscordMusic.Core.Playback;

internal sealed class PlaybackController(
    ITrackQueue trackQueue,
    IYouTubeDownloadScheduler downloadScheduler
) : IPlaybackController
{
    private static readonly Counter<long> PlaybackControlRequests =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.playback.control.requests",
            unit: "1",
            description: "Playback control requests."
        );
    private static readonly Histogram<double> PlaybackSeekPosition =
        DiscordMusicObservability.Meter.CreateHistogram<double>(
            "discord.music.playback.seek.position",
            unit: "s",
            description: "Requested playback seek positions."
        );

    public PlaybackCommandResult Pause(ulong guildId, PlaybackSession session)
    {
        var success = session.RequestPause();
        RecordPlaybackControl(guildId, "pause", success);
        return success
            ? PlaybackCommandResult.Success("Paused playback.")
            : PlaybackCommandResult.Failure(
                "Nothing is currently playing, or playback is already paused."
            );
    }

    public PlaybackCommandResult Resume(ulong guildId, PlaybackSession session)
    {
        var success = session.RequestResume();
        RecordPlaybackControl(guildId, "resume", success);
        return success
            ? PlaybackCommandResult.Success("Resumed playback.")
            : PlaybackCommandResult.Failure("Playback is not paused.");
    }

    public PlaybackCommandResult Seek(ulong guildId, PlaybackSession session, TimeSpan position)
    {
        var snapshot = session.Snapshot();
        if (snapshot.CurrentTrack is not { } currentTrack)
        {
            RecordPlaybackControl(guildId, "seek", success: false);
            return PlaybackCommandResult.Failure("Nothing is currently playing.");
        }

        if (currentTrack.Duration > TimeSpan.Zero && position >= currentTrack.Duration)
        {
            RecordPlaybackControl(guildId, "seek", success: false);
            return PlaybackCommandResult.Failure(
                "Seek position must be before the end of the track."
            );
        }

        var success = session.RequestSeek(position);
        RecordPlaybackControl(guildId, "seek", success);
        if (success)
        {
            PlaybackSeekPosition.Record(
                position.TotalSeconds,
                DiscordMusicObservability.GuildTags(guildId)
            );
        }

        return success
            ? PlaybackCommandResult.Success($"Seeking to {position.HumanizeSecond()}.")
            : PlaybackCommandResult.Failure("Nothing is currently playing.");
    }

    public async Task<PlaybackCommandResult> SkipAsync(
        ulong guildId,
        PlaybackSession session,
        CancellationToken cancellationToken
    )
    {
        if (session.RequestSkip())
        {
            RecordPlaybackControl(guildId, "skip", success: true);
            await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
            return PlaybackCommandResult.Success("Skipped the current track.");
        }

        if (
            trackQueue.TryRemoveFirstNonFailed(guildId, out var queuedTrack)
            && queuedTrack is { } item
        )
        {
            RecordPlaybackControl(guildId, "skip_queued", success: true);
            await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
            return PlaybackCommandResult.Success($"Skipped queued track: {item.Track.Name}.");
        }

        RecordPlaybackControl(guildId, "skip", success: false);
        return PlaybackCommandResult.Failure("Nothing is playing and the queue is empty.");
    }

    public async Task<PlaybackCommandResult> SkipToAsync(
        ulong guildId,
        PlaybackSession session,
        int queueIndex,
        CancellationToken cancellationToken
    )
    {
        if (queueIndex < 1)
        {
            RecordPlaybackControl(guildId, "skip_to", success: false);
            return PlaybackCommandResult.Failure("Queue index must be 1 or higher.");
        }

        var queuedTracks = trackQueue.QueuedTracks(guildId);
        if (queueIndex > queuedTracks.Count)
        {
            RecordPlaybackControl(guildId, "skip_to", success: false);
            return PlaybackCommandResult.Failure(
                $"Queue index {queueIndex} is out of range. The queue has {queuedTracks.Count} track(s)."
            );
        }

        var target = queuedTracks[queueIndex - 1];
        if (queueIndex > 1)
        {
            trackQueue.SkipTo(guildId, queueIndex - 1);
        }

        var skippedCurrent = session.RequestSkip();
        RecordPlaybackControl(guildId, "skip_to", success: true);
        await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);

        return skippedCurrent
            ? PlaybackCommandResult.Success(
                $"Skipped to queue item {queueIndex}: {target.Track.Name}."
            )
            : PlaybackCommandResult.Success(
                $"Queue advanced to item {queueIndex}: {target.Track.Name}."
            );
    }

    public PlaybackCommandResult Stop(ulong guildId, PlaybackSession session)
    {
        trackQueue.Clear(guildId);
        var success = session.RequestStop();
        RecordPlaybackControl(guildId, "stop", success);
        return success
            ? PlaybackCommandResult.Success("Stopped playback and cleared the queue.")
            : PlaybackCommandResult.Success("Queue cleared. Nothing was playing.");
    }

    private static void RecordPlaybackControl(ulong guildId, string control, bool success)
    {
        var tags = DiscordMusicObservability.GuildTags(guildId);
        tags.Add("music.playback.control", control);
        tags.Add("result", success ? "accepted" : "rejected");

        PlaybackControlRequests.Add(1, tags);
    }
}
