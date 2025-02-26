using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using ErrorOr;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Lyrics;

internal partial class LyricsSearch(ILogger<LyricsSearch> logger, IOptions<LyricsOptions> lyricOptions) : ILyricsSearch
{
    public async Task<ErrorOr<Lyrics>> SearchAsync(string title, string artist, CancellationToken ct)
    {
        logger.LogDebug("Search lyrics for {Title} - {Artist}", title, artist);

        if (string.IsNullOrWhiteSpace(lyricOptions.Value.Token))
        {
            logger.LogWarning("Token required to search for lyrics");
            return Error.Validation(description: "Token required to search for lyrics");
        }

        var searchResponse = await "https://api.genius.com/search"
            .SetQueryParam("q", $"{title} {artist}")
            .WithOAuthBearerToken(lyricOptions.Value.Token)
            .GetJsonAsync<SearchResponse>(cancellationToken: ct);

        if (
            searchResponse.Response.Hits == null
            || searchResponse.Response.Hits.Count == 0
            || searchResponse.Response.Hits.All(hit => hit.Type != "song")
        )
        {
            logger.LogWarning("No lyrics found for {Title} - {Artist}", title, artist);
            return Error.NotFound(description: $"No lyrics found for {title} - {artist}");
        }

        logger.LogDebug("Lyrics found for {Title} - {Artist}", title, artist);

        var firstHit = BestMatchingHit(searchResponse.Response.Hits, title, artist);
        var lyricsPageUrl = new Url($"https://genius.com{firstHit.Result.Path}");
        var lyrics = await ScrapeLyricsAsync(logger, lyricsPageUrl, ct);

        if (lyrics.IsError)
        {
            return lyrics.Errors;
        }

        return new Lyrics(firstHit.Result.Title, firstHit.Result.ArtistNames, lyrics.Value, lyricsPageUrl);
    }

    private static Hit BestMatchingHit(List<Hit> hits, string title, string artist)
    {
        return hits.Where(hit => hit.Type == "song")
            .Select(hit => new { hit, score = Score(hit, title, artist) })
            .OrderBy(x => x.score.Levenstein)
            .ThenByDescending(x => x.score.CommonChars)
            .First()
            .hit;
    }

    private static (int Levenstein, int CommonChars) Score(Hit hit, string title, string artist)
    {
        var expected = $"{title} {artist}";
        var got = $"{hit.Result.Title} {hit.Result.ArtistNames}";

        var levenstein = LevenshteinDistance(expected, got);
        var commonChars = expected.Intersect(got).Count();

        return (Levenstein: levenstein, CommonChars: commonChars);
    }

    private static int LevenshteinDistance(string expected, string got)
    {
        var m = expected.Length;
        var n = got.Length;
        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= n; j++)
        {
            d[0, j] = j;
        }

        for (var j = 1; j <= n; j++)
        {
            for (var i = 1; i <= m; i++)
            {
                var cost = expected[i - 1] == got[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }

    private static async Task<ErrorOr<string>> ScrapeLyricsAsync(
        ILogger<LyricsSearch> logger,
        Url url,
        CancellationToken ct
    )
    {
        logger.LogDebug("Scraping lyrics from {Url}", url);

        var lyricsPage = await url.GetStringAsync(cancellationToken: ct);
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(lyricsPage);
        var lyricsDivs = htmlDoc.DocumentNode.SelectNodes("//div[@data-lyrics-container='true']");

        if (lyricsDivs == null || lyricsDivs.Count == 0)
        {
            logger.LogWarning("Lyrics not found on the page {Url}", url);
            return Error.NotFound(description: $"Lyrics not found on the page {url}");
        }

        var lyricsBuilder = new StringBuilder();
        foreach (var div in lyricsDivs)
        {
            var htmlContent = div.InnerHtml;
            var decodedContent = HttpUtility.HtmlDecode(htmlContent);
            var newlineContent = ReplaceBrWithNewLine().Replace(decodedContent, Environment.NewLine);
            var textContent = RemoveNonText().Replace(newlineContent, string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(textContent))
            {
                lyricsBuilder.AppendLine(textContent);
            }
        }

        return lyricsBuilder.ToString().Trim();
    }

    [GeneratedRegex(@"<br\s*/?>")]
    private static partial Regex ReplaceBrWithNewLine();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RemoveNonText();

    private readonly record struct Result(
        [property: JsonPropertyName("artist_names")] string ArtistNames,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("path")] string Path
    );

    private readonly record struct Hit(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("result")] Result Result
    );

    private readonly record struct Response([property: JsonPropertyName("hits")] List<Hit>? Hits);

    private readonly record struct SearchResponse([property: JsonPropertyName("response")] Response Response);
}
