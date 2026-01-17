using System.Threading.Channels;

namespace DiscordMusic.Core.V4;

internal class Queue
{
    private CancellationTokenSource? _activeTrackCts;
    private readonly List<MusicTrack> _tracks = new();
    private readonly object _lock = new();

    // Channels to communicate with background workers
    // Bounded channels provide built-in backpressure
    private readonly Channel<MusicTrack> _downloadChannel = Channel.CreateBounded<MusicTrack>(10);
    private readonly Channel<MusicTrack> _playerChannel = Channel.CreateUnbounded<MusicTrack>();

    public ChannelReader<MusicTrack> DownloadRequests => _downloadChannel.Reader;
    public ChannelReader<MusicTrack> PlaybackRequests => _playerChannel.Reader;
    
    public async Task AddTrackAsync(MusicTrack track)
    {
        lock (_lock)
        {
            _tracks.Add(track);
        }

        // Tell the Downloader to start getting the file
        await _downloadChannel.Writer.WriteAsync(track);
    }

    public void NotifyTrackReady(MusicTrack track)
    {
        track.IsReady = true;
        // If this is the next song to be played, tell the Player
        _playerChannel.Writer.TryWrite(track);
    }
    
    public void NotifyTrackFault(MusicTrack track)
    {
        
    }

    public List<MusicTrack> GetQueue() 
    {
        lock (_lock) return _tracks.ToList();
    }

    public void Skip()
    {
        // Logic to cancel the current CancellationToken in the PlayerService
    }
    
    // Call this from your Discord Command (e.g., !stop or !skip)
    public void StopCurrentTrack()
    {
        _activeTrackCts?.Cancel();
    }

    // The PlayerService will call this to get a token for the new track
    public CancellationToken StartNewTrackToken()
    {
        _activeTrackCts?.Cancel(); // Cancel any existing playback first
        _activeTrackCts = new CancellationTokenSource();
        return _activeTrackCts.Token;
    }
}
