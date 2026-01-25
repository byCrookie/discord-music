namespace DiscordMusic.Core.Discord.VoiceCommands;

public interface IVoiceTranscriber
{
    /// <summary>
    /// Transcribe a complete audio buffer.
    /// Input must be 16kHz mono 16-bit PCM.
    /// </summary>
    Task<string> TranscribeAsync(ReadOnlyMemory<byte> pcm16kMonoS16, CancellationToken ct);
}
