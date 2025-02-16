using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Lyrics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class LyricsAction(
    IVoiceHost voiceHost,
    Replier replier,
    ILogger<LyricsAction> logger,
    ILyricsSearch lyricsSearch
) : IDiscordAction
{
    public string Long => "lyrics";
    public string Short => "ly";

    public string Help =>
        """
            Displays the lyrics of the current track. It also possible to search for lyrics of a specific track.
            Usage: `lyrics | lyrics <title> - <artists>`
            - `<title>`: The title of the track
            - `<artists>`: The artists of the track
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Lyrics");
        
        var nowPlaying = await voiceHost.NowPlayingAsync(message, ct);

        if (nowPlaying.IsError)
        {
            return nowPlaying.Errors;
        }

        if (args.Length != 0)
        {
            var title = string.Join(" ", args);
            var split = title.Split("-", StringSplitOptions.RemoveEmptyEntries);

            if (split.Length != 2)
            {
                await replier
                    .Reply()
                    .To(message)
                    .SendErrorAsync("Invalid format. Usage: `lyrics <title> - <artists>`", ct);
                
                return Result.Success;
            }

            var specificLyrics = await lyricsSearch.SearchAsync(split[0], split[1], ct);

            if (specificLyrics.IsError)
            {
                await replier
                    .Reply()
                    .To(message)
                    .WithEmbed("Lyrics", $"Lyrics not found for {split[0]} by {split[1]}")
                    .WithDeletion()
                    .SendAsync(ct);
                
                return Result.Success;
            }

            var specificLyricsMessage = $"""
                                         {specificLyrics.Value.Text}

                                         {specificLyrics.Value.Url}
                                         """;
            
            await replier
                .Reply()
                .To(message)
                .WithEmbed($"**{specificLyrics.Value.Title}** by **{specificLyrics.Value.Artist}**", specificLyricsMessage)
                .WithDeletion(TimeSpan.FromMinutes(5))
                .SendAsync(ct);
            
            return Result.Success;
        }
        
        if (nowPlaying.Value.Track is null)
        {
            await replier
                .Reply()
                .To(message)
                .WithEmbed("Lyrics", "No track is currently playing")
                .WithDeletion()
                .SendAsync(ct);
            
            return Result.Success;
        }

        var track = nowPlaying.Value.Track;

        var lyrics = await lyricsSearch.SearchAsync(track.Name, track.Artists, ct);

        if (lyrics.IsError)
        {
            await replier
                .Reply()
                .To(message)
                .WithEmbed("Lyrics", $"Lyrics not found for {track.Name} by {track.Artists}")
                .WithDeletion()
                .SendAsync(ct);
            
            return Result.Success;
        }

        var lyricsMessage = $"""
            {lyrics.Value.Text}

            {lyrics.Value.Url}
            """;
        
        await replier
            .Reply()
            .To(message)
            .WithEmbed($"**{lyrics.Value.Title}** by **{lyrics.Value.Artist}**", lyricsMessage)
            .WithDeletion(GetDeletionDelayFromNowPlaying(nowPlaying))
            .SendAsync(ct);
        
        return Result.Success;
    }

    private static TimeSpan GetDeletionDelayFromNowPlaying(ErrorOr<VoiceUpdate> nowPlaying)
    {
        var remaining = nowPlaying.Value.AudioStatus.Length - nowPlaying.Value.AudioStatus.Position;
        return remaining < TimeSpan.Zero ? TimeSpan.FromMinutes(10) : remaining;
    }
}
