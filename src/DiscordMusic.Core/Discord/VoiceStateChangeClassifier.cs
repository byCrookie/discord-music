namespace DiscordMusic.Core.Discord;

internal static class VoiceStateChangeClassifier
{
    public static VoiceStateChange Classify(
        ulong userId,
        ulong botId,
        ulong? previousChannelId,
        ulong? currentChannelId
    )
    {
        // Currently the classification rules are identical for bot and users,
        // but we keep the botId parameter in the API so callers can evolve bot-specific
        // rules without changing call sites.

        if (previousChannelId is null && currentChannelId is not null)
        {
            return VoiceStateChange.Joined;
        }

        if (previousChannelId is not null && currentChannelId is null)
        {
            return VoiceStateChange.Left;
        }

        if (
            previousChannelId is not null
            && currentChannelId is not null
            && previousChannelId != currentChannelId
        )
        {
            return VoiceStateChange.Moved;
        }

        return VoiceStateChange.Unknown;
    }
}
