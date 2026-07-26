using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Flurl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusic.Core.Spotify;

internal class SpotifySearch(
    ILogger<SpotifySearch> logger,
    IOptions<SpotifyOptions> spotifyOptions,
    SpotifyClientConfig spotifyClientConfig,
    TimeProvider timeProvider
) : ISpotifySearch
{
    private const string SpotifyDomain = "open.spotify.com";
    private const string SpotifyApiDomain = "api.spotify.com";
    private static readonly Counter<long> SpotifyUrlSearches =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.spotify.url_searches",
            unit: "1",
            description: "Spotify URL searches by URL type."
        );

    public bool IsSpotifyQuery(string query)
    {
        return Url.IsValid(query)
            && (query.Contains(SpotifyDomain) || query.Contains(SpotifyApiDomain));
    }

    public async Task<ErrorOr<List<SpotifyTrack>>> SearchAsync(string query, CancellationToken ct)
    {
        var startedAt = timeProvider.GetTimestamp();
        var result = "completed";
        using var activity = DiscordMusicObservability.StartActivity(
            "spotify.search",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetTag(activity, "music.search.query.length", query.Length);
        DiscordMusicObservability.SetTag(
            activity,
            "music.search.query.kind",
            Url.IsValid(query) ? "url" : "text"
        );

        logger.LogDebug("Searching Spotify for {Query}.", query);

        if (
            string.IsNullOrWhiteSpace(spotifyOptions.Value.ClientId)
            || string.IsNullOrWhiteSpace(spotifyOptions.Value.ClientSecret)
        )
        {
            result = "validation_failed";
            activity?.SetStatus(ActivityStatusCode.Error, result);
            logger.LogError("Spotify client id and secret not set");
            return Error.Validation(description: "Spotify client id and secret not set");
        }

        var config = spotifyClientConfig.WithAuthenticator(
            new ClientCredentialsAuthenticator(
                spotifyOptions.Value.ClientId,
                spotifyOptions.Value.ClientSecret
            )
        );

        var spotify = new SpotifyClient(config);

        try
        {
            var tracks = Url.IsValid(query)
                ? await SearchByUrlAsync(spotify, new Url(query), ct).ToListAsync(ct)
                : await SearchByQueryAsync(spotify, query, ct).ToListAsync(ct);
            DiscordMusicObservability.SetTag(activity, "music.track.count", tracks.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return tracks;
        }
        catch (ArgumentException e)
        {
            result = "validation_failed";
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            logger.LogDebug(e, "Invalid Spotify URL {Query}", query);
            return Error.Validation(description: e.Message);
        }
        catch (APITooManyRequestsException e)
        {
            result = "rate_limited";
            activity?.SetStatus(ActivityStatusCode.Error, result);
            activity?.AddException(e);
            DiscordMusicObservability.ExternalRateLimits.Add(
                1,
                DiscordMusicObservability.ExternalRequestTags(
                    "spotify",
                    "spotify.search",
                    "rate_limited"
                )
            );
            logger.LogWarning(e, "Spotify API rate limit exceeded");
            return Error
                .Unexpected(
                    code: "Spotify.RateLimited",
                    description: "Spotify is currently unavailable."
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "spotify.search")
                .WithMetadata("retryAfter", e.RetryAfter.TotalSeconds)
                .WithException(e);
        }
        catch (APIException e)
        {
            result = "api_error";
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            var responseBody = e.Response?.Body?.ToString();
            var statusCode = e.Response?.StatusCode;
            DiscordMusicObservability.SetTag(activity, "http.response.status_code", statusCode);

            if (IsPremiumRequiredResponse(statusCode?.ToString(), responseBody))
            {
                result = "premium_required";
                logger.LogWarning(
                    e,
                    "Spotify API rejected the request because the app owner account requires Premium. Query={Query}",
                    query
                );
                return Error
                    .Forbidden(
                        code: "Spotify.PremiumRequired",
                        description: "Spotify rejected this request because the account that owns the configured Spotify app needs an active Premium subscription. Check the Spotify account used for `spotify:clientId` and `spotify:clientSecret`, then retry after Spotify has propagated any subscription changes."
                    )
                    .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "spotify.search")
                    .WithMetadata("query", query)
                    .WithMetadata("responseBody", responseBody)
                    .WithMetadata("statusCode", statusCode)
                    .WithException(e);
            }

            logger.LogError(
                e,
                "Spotify API error. Query={Query} StatusCode={StatusCode} ResponseBody={ResponseBody}",
                query,
                statusCode,
                responseBody
            );
            return Error
                .Unexpected(
                    code: "Spotify.ApiError",
                    description: "Spotify rejected the request. Check the details for the response from Spotify."
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "spotify.search")
                .WithMetadata("query", query)
                .WithMetadata("responseBody", responseBody)
                .WithMetadata("statusCode", statusCode)
                .WithException(e);
        }
        catch (Exception e)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            logger.LogError(e, "Spotify search failed. Query={Query}", query);
            return Error
                .Unexpected(code: "Spotify.SearchFailed", description: "Spotify search failed.")
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "spotify.search")
                .WithMetadata("query", query)
                .WithException(e);
        }
        finally
        {
            var tags = DiscordMusicObservability.ExternalRequestTags(
                "spotify",
                "spotify.search",
                result
            );
            DiscordMusicObservability.ExternalRequests.Add(1, tags);
            DiscordMusicObservability.ExternalRequestDuration.Record(
                timeProvider.GetElapsedTime(startedAt).TotalSeconds,
                tags
            );
        }
    }

    internal static bool IsPremiumRequiredResponse(string? statusCode, string? responseBody)
    {
        return IsForbiddenStatus(statusCode)
            && responseBody?.Contains(
                "Active premium subscription required",
                StringComparison.OrdinalIgnoreCase
            ) == true;
    }

    private static bool IsForbiddenStatus(string? statusCode)
    {
        return string.Equals(statusCode, "Forbidden", StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusCode, "403", StringComparison.OrdinalIgnoreCase);
    }

    private static async IAsyncEnumerable<SpotifyTrack> SearchByQueryAsync(
        SpotifyClient spotify,
        string query,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        var search = await spotify.Search.Item(
            new SearchRequest(SearchRequest.Types.Track, query),
            ct
        );
        await foreach (var track in spotify.Paginate(search.Tracks, s => s.Tracks, cancel: ct))
        {
            yield return new SpotifyTrack(
                track.Name,
                BuildArtists(track.Artists),
                new Url(track.Href)
            );
        }
    }

    private static async IAsyncEnumerable<SpotifyTrack> SearchByUrlAsync(
        SpotifyClient client,
        Url url,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        if (url.Host != SpotifyDomain)
        {
            throw new ArgumentException(
                $"Url {url} is not a Spotify url (host is not {SpotifyDomain})"
            );
        }

        if (!TryParseSpotifyUrl(url, out var spotifyUrl))
        {
            throw new NotSupportedException(
                $"Unknown url type {url}. Supported types are track, playlist, album, artist"
            );
        }

        SpotifyUrlSearches.Add(
            1,
            new KeyValuePair<string, object?>("server.address", "spotify"),
            new KeyValuePair<string, object?>("spotify.url.type", spotifyUrl.Type.ToString())
        );

        switch (spotifyUrl.Type)
        {
            case SpotifyUrlType.Track:
                var track = await client.Tracks.Get(spotifyUrl.Id, ct);
                yield return new SpotifyTrack(
                    track.Name,
                    BuildArtists(track.Artists),
                    new Url(track.Href)
                );
                yield break;
            case SpotifyUrlType.Playlist:
                var playlist = await client.Playlists.GetPlaylistItems(spotifyUrl.Id, ct);
                await foreach (var item in client.Paginate(playlist, cancel: ct))
                {
                    yield return item.Track switch
                    {
                        FullTrack playlistTrack => new SpotifyTrack(
                            playlistTrack.Name,
                            BuildArtists(playlistTrack.Artists),
                            new Url(playlistTrack.Href)
                        ),
                        FullEpisode episode => new SpotifyTrack(
                            episode.Name,
                            episode.Show.Name,
                            new Url(episode.Href)
                        ),
                        _ => throw new NotSupportedException($"Unknown item type {item.GetType()}"),
                    };
                }

                yield break;
            case SpotifyUrlType.Album:
                var album = await client.Albums.GetTracks(spotifyUrl.Id, ct);
                await foreach (var albumTrack in client.Paginate(album, cancel: ct))
                {
                    yield return new SpotifyTrack(
                        albumTrack.Name,
                        BuildArtists(albumTrack.Artists),
                        new Url(albumTrack.Href)
                    );
                }

                yield break;
            case SpotifyUrlType.Artist:
                var artist = await client.Artists.Get(spotifyUrl.Id, ct);
                var search = await client.Search.Item(
                    new SearchRequest(SearchRequest.Types.Track, $"artist:{artist.Name}"),
                    ct
                );

                await foreach (
                    var topTrack in client.Paginate(search.Tracks, s => s.Tracks, cancel: ct)
                )
                {
                    yield return new SpotifyTrack(
                        topTrack.Name,
                        BuildArtists(topTrack.Artists),
                        new Url(topTrack.Href)
                    );
                }

                yield break;
            default:
                throw new ArgumentOutOfRangeException(nameof(spotifyUrl.Type));
        }
    }

    internal static bool TryParseSpotifyUrl(Url url, out SpotifyUrlReference spotifyUrl)
    {
        if (url.PathSegments.Count < 2 || string.IsNullOrWhiteSpace(url.PathSegments[1]))
        {
            spotifyUrl = default;
            return false;
        }

        var id = url.PathSegments[1];
        var type = url.PathSegments[0].ToLowerInvariant() switch
        {
            "track" => SpotifyUrlType.Track,
            "playlist" => SpotifyUrlType.Playlist,
            "album" => SpotifyUrlType.Album,
            "artist" => SpotifyUrlType.Artist,
            _ => (SpotifyUrlType?)null,
        };

        if (type is null)
        {
            spotifyUrl = default;
            return false;
        }

        spotifyUrl = new SpotifyUrlReference(type.Value, id);
        return true;
    }

    private static string BuildArtists(IEnumerable<SimpleArtist> artists)
    {
        return string.Join(" & ", artists.Select(a => a.Name));
    }

    internal enum SpotifyUrlType
    {
        Track,
        Playlist,
        Album,
        Artist,
    }

    internal readonly record struct SpotifyUrlReference(SpotifyUrlType Type, string Id);
}
