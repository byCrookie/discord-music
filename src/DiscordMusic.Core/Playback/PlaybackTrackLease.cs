namespace DiscordMusic.Core.Playback;

internal sealed class PlaybackTrackLease(
    PlaybackSession session,
    CancellationTokenSource cancellationTokenSource
) : IDisposable
{
    public CancellationToken CancellationToken => cancellationTokenSource.Token;

    public void Dispose()
    {
        session.EndTrack(cancellationTokenSource);
        cancellationTokenSource.Dispose();
    }
}
