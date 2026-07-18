using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class LyricsAction(
    ILogger<LyricsAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService,
    ILyricsSearch lyricsSearch,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    private const int MaxLyricsLength = 1800;

    [SlashCommand(
        "lyrics",
        "Search lyrics for the current or specified track.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    public async Task<InteractionMessageProperties> Lyrics(
        [SlashCommandParameter(Description = "Song title. Defaults to the current track.")]
            string? title = null,
        [SlashCommandParameter(Description = "Song artist. Defaults to the current track artist.")]
            string? artist = null
    )
    {
        logger.LogTrace("Lyrics");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            if (
                !VoiceCommandGuard.TryGetPlaybackSession(
                    Context,
                    voiceInstances,
                    playbackService,
                    out var session,
                    out _,
                    out var error
                )
            )
            {
                return error;
            }

            var snapshot = session.Snapshot();
            if (snapshot.CurrentTrack is not { } track)
            {
                return DiscordResponses.Ephemeral(
                    "Provide title and artist, or play a track first."
                );
            }

            title ??= track.Name;
            artist ??= track.Artists;
        }

        var search = await lyricsSearch.SearchAsync(title, artist, cancellation.CancellationToken);
        if (search.IsError)
        {
            return DiscordResponses.Ephemeral($"Lyrics not found: {search.ToErrorContent()}");
        }

        var text =
            search.Value.Text.Length > MaxLyricsLength
                ? $"{search.Value.Text[..MaxLyricsLength]}\n..."
                : search.Value.Text;

        return DiscordResponses.Ephemeral(
            $"""
            ### Lyrics for {search.Value.Title} - {search.Value.Artist}
            {text}
            """
        );
    }
}
