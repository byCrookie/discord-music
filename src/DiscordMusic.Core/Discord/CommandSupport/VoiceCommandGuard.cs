using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal static class VoiceCommandGuard
{
    public static bool TryGetGuild(
        ApplicationCommandContext context,
        out ulong guildId,
        out InteractionMessageProperties error
    )
    {
        if (context.Guild is { } guild)
        {
            guildId = guild.Id;
            error = null!;
            return true;
        }

        guildId = 0;
        error = DiscordResponses.Ephemeral("The guild is not available. Try again later.");
        return false;
    }

    public static bool TryGetPlaybackSession(
        ApplicationCommandContext context,
        VoiceConnectionRegistry voiceConnections,
        PlaybackService playbackService,
        out PlaybackSession playbackSession,
        out ulong guildId,
        out InteractionMessageProperties error
    )
    {
        playbackSession = null!;

        if (!TryGetGuild(context, out guildId, out error))
        {
            return false;
        }

        if (
            voiceConnections.Mapping.TryGetValue(guildId, out var voiceConnection)
            && voiceConnection is not null
        )
        {
            playbackSession = voiceConnection.PlaybackSession;
            error = null!;
            return true;
        }

        if (playbackService.TryGetPlaybackSession(guildId, out playbackSession))
        {
            error = null!;
            return true;
        }

        error = DiscordResponses.Ephemeral(
            "I do not have an active playback session for this guild. Use /play or /join from your voice channel."
        );
        return false;
    }
}
