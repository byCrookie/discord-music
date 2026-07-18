using System.Collections.Concurrent;

namespace DiscordMusic.Core.Discord.Voice;

internal class VoiceConnectionRegistry
{
    public readonly ConcurrentDictionary<ulong, VoiceConnection?> Mapping = [];
}
