namespace DiscordMusic.Core.Discord.VoiceCommands;

internal interface IVoiceCommandService
{
    /// <summary>
    /// Start listening for voice and buffering/transcribing commands for this guild.
    /// The caller is responsible for passing the current voice client instance.
    /// Returns a token that unsubscribes from events; dispose it when the voice client changes or the session ends.
    /// </summary>
    IDisposable Subscribe(ulong guildId, NetCord.Gateway.Voice.VoiceClient voiceClient);

    /// <summary>
    /// Stop all voice command processing for a guild (drops buffers/pipeline).
    /// </summary>
    void UnsubscribeGuild(ulong guildId);
}
