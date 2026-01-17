using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class LyricsAction(
    IVoiceHost voiceHost,
    ILogger<LyricsAction> logger,
    ILyricsSearch lyricsSearch,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "lyrics",
        "Search lyrics for a specific track. Leave blank to get lyrics for the currently playing track."
    )]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Lyrics(string? title = null, string? artists = null)
    {
        var ct = cancellation.CancellationToken;
        logger.LogTrace("Lyrics");

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Searching for lyrics of **{title} - {artists}**
                    This may take a moment...
                    """,
                }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        var nowPlaying = await voiceHost.NowPlayingAsync(Context, ct);

        if (nowPlaying.IsError)
        {
            await ModifyResponseAsync(
                m => m.Content = nowPlaying.ToContent(),
                cancellationToken: ct
            );
            return;
        }

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artists))
        {
            await ModifyResponseAsync(
                m =>
                    m.Content = $"""
                    ### Searching for lyrics of **{title} - {artists}**
                    This may take a moment...
                    """,
                cancellationToken: ct
            );

            var specificLyrics = await lyricsSearch.SearchAsync(title, artists, ct);

            if (specificLyrics.IsError)
            {
                await ModifyResponseAsync(
                    m => m.Content = specificLyrics.ToContent(),
                    cancellationToken: ct
                );
                return;
            }

            await ModifyResponseAsync(
                m =>
                    m.Content = $"""
                    ### **{specificLyrics.Value.Title}** by **{specificLyrics.Value.Artist}**
                    {specificLyrics.Value.Text}
                    -# {specificLyrics.Value.Url}
                    """,
                cancellationToken: ct
            );
            return;
        }

        if (nowPlaying.Value.Track is null)
        {
            await ModifyResponseAsync(
                m => m.Content = "No track is currently playing",
                cancellationToken: ct
            );
            return;
        }

        var track = nowPlaying.Value.Track;

        await ModifyResponseAsync(
            m =>
                m.Content = $"""
                ### Searching for lyrics of **{track.Name} - {track.Artists}**
                This may take a moment...
                """,
            cancellationToken: ct
        );

        var lyrics = await lyricsSearch.SearchAsync(track.Name, track.Artists, ct);

        if (lyrics.IsError)
        {
            await ModifyResponseAsync(m => m.Content = lyrics.ToContent(), cancellationToken: ct);
            return;
        }

        await ModifyResponseAsync(
            m =>
                m.Content = $"""
                ### **{lyrics.Value.Title}** by **{lyrics.Value.Artist}**
                {lyrics.Value.Text}
                -# {lyrics.Value.Url}
                """,
            cancellationToken: ct
        );
    }

    private static TimeSpan GetDeletionDelayFromNowPlaying(ErrorOr<VoiceUpdate> nowPlaying)
    {
        var remaining = nowPlaying.Value.AudioStatus.Length - nowPlaying.Value.AudioStatus.Position;
        return remaining < TimeSpan.Zero ? TimeSpan.FromMinutes(10) : remaining;
    }
}
