using System.Text.Json.Serialization;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Lyrics;

internal class LyricsSearch(ILogger<LyricsSearch> logger) : ILyricsSearch
{
    public async Task<ErrorOr<Lyrics>> SearchAsync(
        string title,
        string artist,
        CancellationToken ct
    )
    {
        logger.LogDebug("Search lyrics for {Title} - {Artist}", title, artist);

        try
        {
            var url = "https://api.lyrics.ovh/v1/"
                .AppendPathSegment(artist)
                .AppendPathSegment(title);

            logger.LogInformation("Calling api: {Url}", url);

            var lyricsResponse = await url.WithHeader("Accept", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(10))
                .GetAsync(cancellationToken: ct);

            var lyrics = await lyricsResponse.GetJsonAsync<LyricsResponse>();

            return new Lyrics(title, artist, lyrics.Lyrics);
        }
        catch (FlurlHttpException e)
        {
            var error = await e.GetResponseJsonAsync<LyricsResponseError>();
            logger.LogError(e, "Lyrics API error. Title={Title} Artist={Artist}", title, artist);
            return Error
                .NotFound(code: "Lyrics.ApiError", description: error.Error)
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "lyrics.search")
                .WithMetadata("title", title)
                .WithMetadata("artist", artist)
                .WithMetadata("statusCode", e.StatusCode)
                .WithException(e);
        }
        catch (Exception e)
        {
            logger.LogError(
                e,
                "Lyrics search failed. Title={Title} Artist={Artist}",
                title,
                artist
            );
            return Error
                .NotFound(code: "Lyrics.SearchFailed", description: "Failed to search for lyrics.")
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "lyrics.search")
                .WithMetadata("title", title)
                .WithMetadata("artist", artist)
                .WithException(e);
        }
    }

    private readonly record struct LyricsResponse(
        [property: JsonPropertyName("lyrics")] string Lyrics
    );

    private readonly record struct LyricsResponseError(
        [property: JsonPropertyName("error")] string Error
    );
}
