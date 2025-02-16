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
            Displays the lyrics of the current track
            Usage: `lyrics`
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Lyrics");
        var nowPlaying = await voiceHost.NowPlayingAsync(message, ct);

        if (nowPlaying.IsError)
        {
            return nowPlaying.Errors;
        }

        if (nowPlaying.Value.Track is null)
        {
            await replier
                .ReplyTo(message)
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
                .ReplyTo(message)
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
            .ReplyTo(message)
            .WithEmbed($"**{track.Name}** by **{track.Artists}**", lyricsMessage)
            .WithDeletion()
            .SendAsync(ct);
        
        return Result.Success;
    }
}
