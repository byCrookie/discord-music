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

        if (searchResponse.Response.Hits == null || searchResponse.Response.Hits.All(hit => hit.Type != "song"))
        {
            logger.LogWarning("No lyrics found for {Title} - {Artist}", title, artist);
            return Error.NotFound(description: $"No lyrics found for {title} - {artist}");
        }

        logger.LogDebug("Lyrics found for {Title} - {Artist}", title, artist);

        var firstHit = searchResponse.Response.Hits.First(hit => hit.Type == "song");
        var lyricsPageUrl = new Url($"https://genius.com{firstHit.Result.Path}");
        var lyrics = await ScrapeLyricsAsync(logger, lyricsPageUrl, ct);

        if (lyrics.IsError)
        {
            return lyrics.Errors;
        }

        return new Lyrics(firstHit.Result.Title, firstHit.Result.ArtistNames, lyrics.Value, lyricsPageUrl);
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
