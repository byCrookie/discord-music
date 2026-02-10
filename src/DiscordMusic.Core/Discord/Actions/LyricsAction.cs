using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal class LyricsAction(
    GuildSessionManager guildSessionManager,
    ILogger<LyricsAction> logger,
    ILyricsSearch lyricsSearch,
    Cancellation cancellation
) : SafeApplicationCommandModule
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

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild!.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                logger,
                ct
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Searching for lyrics
                    **{title}** — **{artists}**
                    -# This may take a moment...
                    """,
                }
            ),
            logger,
            ct
        );

        var nowPlaying = await session.Value.NowPlayingAsync(ct);

        if (nowPlaying.IsError)
        {
            await SafeModifyResponseAsync(m => m.Content = nowPlaying.ToErrorContent(), logger, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artists))
        {
            await SafeModifyResponseAsync(
                m =>
                    m.Content = $"""
                    ### Searching for lyrics
                    **{title}** — **{artists}**
                    -# This may take a moment...
                    """,
                logger,
                ct
            );

            var specificLyrics = await lyricsSearch.SearchAsync(title, artists, ct);

            if (specificLyrics.IsError)
            {
                await SafeModifyResponseAsync(
                    m => m.Content = specificLyrics.ToErrorContent(),
                    logger,
                    ct
                );
                return;
            }

            await SafeModifyResponseAsync(
                m =>
                    m.Content = $"""
                    ### **{specificLyrics.Value.Title}** by **{specificLyrics.Value.Artist}**
                    {specificLyrics.Value.Text}
                    """,
                logger,
                ct
            );
            return;
        }

        if (nowPlaying.Value.Track is null)
        {
            await SafeModifyResponseAsync(
                m => m.Content = "Nothing is playing right now.",
                logger,
                ct
            );
            return;
        }

        var track = nowPlaying.Value.Track;

        await SafeModifyResponseAsync(
            m =>
                m.Content = $"""
                ### Searching for lyrics
                **{track.Name}** — **{track.Artists}**
                -# This may take a moment...
                """,
            logger,
            ct
        );

        var lyrics = await lyricsSearch.SearchAsync(track.Name, track.Artists, ct);

        if (lyrics.IsError)
        {
            await SafeModifyResponseAsync(m => m.Content = lyrics.ToErrorContent(), logger, ct);
            return;
        }

        await SafeModifyResponseAsync(
            m =>
                m.Content = $"""
                ### **{lyrics.Value.Title}** by **{lyrics.Value.Artist}**
                {lyrics.Value.Text}
                """,
            logger,
            ct
        );
    }
}
