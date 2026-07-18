using DiscordMusic.Core.Playback;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.Voice;

internal sealed class VoiceConnection(VoiceClient client) : IDisposable
{
    private static readonly int JobTypeCount = Enum.GetValues<VoiceJobType>().Length;

    public VoiceClient Client => client;
    public PlaybackSession PlaybackSession { get; } = new();

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    private readonly byte[] _jobStatuses = new byte[JobTypeCount];

    public Job? TryEnterJob(VoiceJobType type)
    {
        return Interlocked.CompareExchange(ref _jobStatuses[(int)type], 1, 0) is 0
            ? new(this, type, _cancellationTokenSource.Token)
            : null;
    }

    public void Dispose()
    {
        var tokenSource = _cancellationTokenSource;
        tokenSource.Cancel();
        tokenSource.Dispose();
        client.Dispose();
    }

    public readonly record struct Job(
        VoiceConnection Instance,
        VoiceJobType JobType,
        CancellationToken CancellationToken
    ) : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Exchange(ref Instance._jobStatuses[(int)JobType], 0);
        }
    }
}

internal enum VoiceJobType
{
    Playing = 0,
    Recording = 1,
}
