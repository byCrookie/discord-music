using DiscordMusic.Core.Utils.Json;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Music.Lyrics;

internal class LyricsService(ILogger<LyricsService> logger, IJsonSerializer jsonSerializer) : ILyricsService
{
    public async Task<Lyrics?> GetLyricsAsync(string title, string author)
    {
        logger.LogDebug("Get lyrics for {Title} - {Author}", title, author);

        var lyrics = await "https://lyrist.vercel.app/api"
            .AppendPathSegment(title)
            .AppendPathSegment(author)
            .GetJsonAsync<Lyrics?>();

        logger.LogDebug("{Lyrics}", jsonSerializer.Serialize(lyrics));

        if (!string.IsNullOrWhiteSpace(lyrics?.Text))
        {
            return lyrics;
        }

        logger.LogWarning("No lyrics found for {Title} - {Author}", title, author);
        return null;
    }
}
