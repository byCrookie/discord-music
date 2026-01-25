using DiscordMusic.Core.Queue;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;

namespace DiscordMusic.Core.Rewrite;

public class MusicSession(
    ILogger<MusicSession> logger,
    IQueue<AudioMetadata> queue,
    Guild guild,
    TextChannel textChannel,
    VoiceSession voiceSession)
{
    private readonly AsyncLock _commandLock = new();
    public ILogger<MusicSession> Logger { get; } = logger;
    
    public IQueue<AudioMetadata> Queue { get; } = queue;

    public Guild Guild { get; } = guild;
    public TextChannel TextChannel { get; } = textChannel;

    public VoiceSession VoiceSession { get; set; } = voiceSession;

    public AudioMetadata? CurrentSong { get; set; }
    public bool IsPlaying { get; set; }

    public async Task ExecuteCommandAsync(Func<Task> command, CancellationToken ct)
    {
        await using var _ = await _commandLock.AquireAsync(ct);
        await command();
    }
}
