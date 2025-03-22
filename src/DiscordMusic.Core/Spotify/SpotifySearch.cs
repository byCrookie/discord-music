using System.Globalization;
using System.Runtime.CompilerServices;
using ErrorOr;
using Flurl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusic.Core.Spotify;

internal class SpotifySearch(
    ILogger<SpotifySearch> logger,
    IOptions<SpotifyOptions> spotifyOptions,
    SpotifyClientConfig spotifyClientConfig
) : ISpotifySearch
{
    private const string SpotifyDomain = "open.spotify.com";
    private const string SpotifyApiDomain = "api.spotify.com";

    public bool IsSpotifyQuery(string query)
    {
        return Url.IsValid(query) && (query.Contains(SpotifyDomain) || query.Contains(SpotifyApiDomain));
    }

    public async Task<ErrorOr<List<SpotifyTrack>>> SearchAsync(string query, CancellationToken ct)
    {
        logger.LogDebug("Searching Spotify for {Query}.", query);

        if (
            string.IsNullOrWhiteSpace(spotifyOptions.Value.ClientId)
            || string.IsNullOrWhiteSpace(spotifyOptions.Value.ClientSecret)
        )
        {
            logger.LogError("Spotify client id and secret not set");
            return Error.Validation(description: "Spotify client id and secret not set");
        }

        var config = spotifyClientConfig.WithAuthenticator(
            new ClientCredentialsAuthenticator(spotifyOptions.Value.ClientId, spotifyOptions.Value.ClientSecret)
        );

        var spotify = new SpotifyClient(config);

        if (Url.IsValid(query))
        {
            return await SearchByUrlAsync(spotify, new Url(query), ct).ToListAsync(ct);
        }

        return await SearchByQueryAsync(spotify, query, ct).ToListAsync(ct);
    }

    private static async IAsyncEnumerable<SpotifyTrack> SearchByQueryAsync(
        SpotifyClient spotify,
        string query,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        var search = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, query), ct);
        await foreach (var track in spotify.Paginate(search.Tracks, s => s.Tracks, cancel: ct))
        {
            yield return new SpotifyTrack(track.Name, BuildArtists(track.Artists), new Url(track.Href));
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
            throw new ArgumentException($"Url {url} is not a Spotify url (host is not {SpotifyDomain})");
        }

        if (url.Path.Contains("track"))
        {
            var trackId = url.PathSegments.Last();
            var track = await client.Tracks.Get(trackId, ct);
            yield return new SpotifyTrack(track.Name, BuildArtists(track.Artists), new Url(track.Href));
            yield break;
        }

        if (url.Path.Contains("playlist"))
        {
            var playlistId = url.PathSegments.Last();
            var playlist = await client.Playlists.GetItems(playlistId, ct);
            await foreach (var item in client.Paginate(playlist, cancel: ct))
            {
                yield return item.Track switch
                {
                    FullTrack track => new SpotifyTrack(track.Name, BuildArtists(track.Artists), new Url(track.Href)),
                    FullEpisode episode => new SpotifyTrack(
                        episode.Name,
                        episode.Show.Publisher,
                        new Url(episode.Href)
                    ),
                    _ => throw new NotSupportedException($"Unknown item type {item.GetType()}"),
                };
            }

            yield break;
        }

        if (url.Path.Contains("album"))
        {
            var albumId = url.PathSegments.Last();
            var album = await client.Albums.GetTracks(albumId, ct);
            await foreach (var track in client.Paginate(album, cancel: ct))
            {
                yield return new SpotifyTrack(track.Name, BuildArtists(track.Artists), new Url(track.Href));
            }

            yield break;
        }

        if (url.Path.Contains("artist"))
        {
            var artistId = url.PathSegments.Last();
            var artistTopTracksRequest = new ArtistsTopTracksRequest(
                CultureInfo.CurrentCulture.TwoLetterISOLanguageName
            );
            var topTracks = await client.Artists.GetTopTracks(artistId, artistTopTracksRequest, ct);
            foreach (var topTrack in topTracks.Tracks)
            {
                yield return new SpotifyTrack(topTrack.Name, BuildArtists(topTrack.Artists), new Url(topTrack.Href));
            }

            yield break;
        }

        throw new NotSupportedException($"Unknown url type {url}. Supported types are track, playlist, album, artist");
    }

    private static string BuildArtists(IEnumerable<SimpleArtist> artists)
    {
        return string.Join(" & ", artists.Select(a => a.Name));
    }
}
