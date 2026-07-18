using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Downloading;

namespace DiscordMusic.Core.Playback;

internal sealed class PlaybackController(
    ITrackQueue trackQueue,
    IYouTubeDownloadScheduler downloadScheduler
) : IPlaybackController
{
    public PlaybackCommandResult Pause(PlaybackSession session)
    {
        return session.RequestPause()
            ? PlaybackCommandResult.Success("Paused playback.")
            : PlaybackCommandResult.Failure(
                "Nothing is currently playing, or playback is already paused."
            );
    }

    public PlaybackCommandResult Resume(PlaybackSession session)
    {
        return session.RequestResume()
            ? PlaybackCommandResult.Success("Resumed playback.")
            : PlaybackCommandResult.Failure("Playback is not paused.");
    }

    public PlaybackCommandResult Seek(PlaybackSession session, TimeSpan position)
    {
        var snapshot = session.Snapshot();
        if (snapshot.CurrentTrack is not { } currentTrack)
        {
            return PlaybackCommandResult.Failure("Nothing is currently playing.");
        }

        if (currentTrack.Duration > TimeSpan.Zero && position >= currentTrack.Duration)
        {
            return PlaybackCommandResult.Failure(
                "Seek position must be before the end of the track."
            );
        }

        return session.RequestSeek(position)
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
            await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
            return PlaybackCommandResult.Success("Skipped the current track.");
        }

        if (
            trackQueue.TryRemoveFirstNonFailed(guildId, out var queuedTrack)
            && queuedTrack is { } item
        )
        {
            await downloadScheduler.EnsureNextTrackQueuedAsync(guildId, cancellationToken);
            return PlaybackCommandResult.Success($"Skipped queued track: {item.Track.Name}.");
        }

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
            return PlaybackCommandResult.Failure("Queue index must be 1 or higher.");
        }

        var queuedTracks = trackQueue.QueuedTracks(guildId);
        if (queueIndex > queuedTracks.Count)
        {
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
        return session.RequestStop()
            ? PlaybackCommandResult.Success("Stopped playback and cleared the queue.")
            : PlaybackCommandResult.Success("Queue cleared. Nothing was playing.");
    }
}
