using DiscordMusic.Core.Tracks;

namespace DiscordMusic.Core.Playback;

internal sealed class PlaybackSession
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _resumeSignal = CompletedSignal();
    private CancellationTokenSource? _currentTrackCancellation;
    private PlaybackControlRequest _request = PlaybackControlRequest.None;
    private bool _paused;

    public PlaybackState State { get; private set; } = PlaybackState.Idle;
    public Track? CurrentTrack { get; private set; }
    public TimeSpan Position { get; private set; }

    public PlaybackSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new PlaybackSnapshot(State, CurrentTrack, Position);
        }
    }

    public PlaybackTrackLease BeginTrack(
        Track track,
        TimeSpan position,
        CancellationToken cancellationToken
    )
    {
        lock (_lock)
        {
            _currentTrackCancellation?.Cancel();
            _currentTrackCancellation?.Dispose();
            _currentTrackCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            _request = PlaybackControlRequest.None;
            _paused = false;
            _resumeSignal.TrySetResult();
            _resumeSignal = CompletedSignal();
            CurrentTrack = track;
            Position = position;
            State = PlaybackState.Playing;

            return new PlaybackTrackLease(this, _currentTrackCancellation);
        }
    }

    public void EndTrack(CancellationTokenSource cancellationTokenSource)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_currentTrackCancellation, cancellationTokenSource))
            {
                return;
            }

            _currentTrackCancellation = null;
            CurrentTrack = null;
            Position = TimeSpan.Zero;
            _paused = false;
            State = PlaybackState.Idle;
            _resumeSignal.TrySetResult();
            _resumeSignal = CompletedSignal();
        }
    }

    internal readonly record struct PlaybackSnapshot(
        PlaybackState State,
        Track? CurrentTrack,
        TimeSpan Position
    );

    public PlaybackControlRequest ConsumeRequest()
    {
        lock (_lock)
        {
            var request = _request;
            _request = PlaybackControlRequest.None;
            return request;
        }
    }

    public void UpdatePosition(TimeSpan position)
    {
        lock (_lock)
        {
            Position = position;
        }
    }

    public bool RequestSkip()
    {
        return RequestCancellation(
            new PlaybackControlRequest(PlaybackControlRequestType.Skip, TimeSpan.Zero)
        );
    }

    public bool RequestStop()
    {
        return RequestCancellation(
            new PlaybackControlRequest(PlaybackControlRequestType.Stop, TimeSpan.Zero)
        );
    }

    public bool RequestSeek(TimeSpan position)
    {
        return RequestCancellation(
            new PlaybackControlRequest(PlaybackControlRequestType.Seek, position)
        );
    }

    public bool RequestPause()
    {
        lock (_lock)
        {
            if (_currentTrackCancellation is null || _paused)
            {
                return false;
            }

            _paused = true;
            State = PlaybackState.Paused;
            _resumeSignal = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            return true;
        }
    }

    public bool RequestResume()
    {
        TaskCompletionSource resumeSignal;
        lock (_lock)
        {
            if (_currentTrackCancellation is null || !_paused)
            {
                return false;
            }

            _paused = false;
            State = PlaybackState.Playing;
            resumeSignal = _resumeSignal;
            _resumeSignal = CompletedSignal();
        }

        resumeSignal.TrySetResult();
        return true;
    }

    public async Task<bool> WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        var waited = false;
        while (true)
        {
            Task resumeTask;
            lock (_lock)
            {
                if (!_paused)
                {
                    return waited;
                }

                resumeTask = _resumeSignal.Task;
            }

            waited = true;
            await resumeTask.WaitAsync(cancellationToken);
        }
    }

    private bool RequestCancellation(PlaybackControlRequest request)
    {
        CancellationTokenSource? currentCancellation;
        TaskCompletionSource resumeSignal;
        lock (_lock)
        {
            if (_currentTrackCancellation is null)
            {
                return false;
            }

            _request = request;
            _paused = false;
            resumeSignal = _resumeSignal;
            _resumeSignal = CompletedSignal();
            currentCancellation = _currentTrackCancellation;
        }

        resumeSignal.TrySetResult();
        currentCancellation.Cancel();
        return true;
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
