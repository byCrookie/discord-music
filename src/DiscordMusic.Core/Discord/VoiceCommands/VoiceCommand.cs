namespace DiscordMusic.Core.Discord.VoiceCommands;
public sealed record VoiceCommand(VoiceCommandIntent Intent, string? Argument = null)
{
    public static readonly VoiceCommand None = new(VoiceCommandIntent.None);
}
