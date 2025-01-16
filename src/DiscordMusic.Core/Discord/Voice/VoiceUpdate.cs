using DiscordMusic.Core.Audio;

namespace DiscordMusic.Core.Discord.Voice;

public record VoiceUpdate(VoiceUpdateType Type, Track? Track, AudioStatus AudioStatus)
{
    public static VoiceUpdate None(VoiceUpdateType type)
    {
        return new VoiceUpdate(type, null, AudioStatus.Stopped);
    }
}

public enum VoiceUpdateType
{
    Now,
    Next
}
