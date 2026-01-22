namespace DiscordMusic.Core.Discord.VoiceCommands;

public enum VoiceCommandIntent
{
    None = 0,
    Play,
    PlayNext,
    Pause,
    Resume,
    Skip,
    Queue,
    NowPlaying,
    Shuffle,
    QueueClear,
    Lyrics,
    Ping,
}
