namespace DiscordMusic.Core.Discord.VoiceCommands;

public interface IVoiceCommandParser
{
    /// <summary>
    /// Parses a transcript (English) into a high-level voice command.
    /// Return <see cref="VoiceCommand.None"/> if no command is detected.
    /// </summary>
    VoiceCommand Parse(string transcript);
}
