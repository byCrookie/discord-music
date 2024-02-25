using System.Globalization;
using DiscordMusic.Cli.Discord.Options.Spotify;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusic.Cli.Discord.Music.Spotify;

internal class Spotify(IOptions<SpotifyOptions> spotifyOptions) : ISpotify
{
    public Task<List<Track>> GetTracksAsync(string argument)
    {
        var config = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(
                spotifyOptions.Value.ClientId,
                spotifyOptions.Value.ClientSecret
            ));

        var spotify = new SpotifyClient(config);
        return GetByArgumentAsync(spotify, argument);
    }

    private static async Task<List<Track>> GetByArgumentAsync(ISpotifyClient spotify, string argument)
    {
        switch (argument)
        {
            case var _ when argument.Contains("track"):
                var trackId = argument.Split("/").Last().Split("?").First();
                var track = await spotify.Tracks.Get(trackId);
                return [new Track(track.Name, BuildArtists(track.Artists), string.Empty, TimeSpan.Zero, Guid.Empty)];
            case var _ when argument.Contains("playlist"):
                var playlistId = argument.Split("/").Last().Split("?").First();
                return await GetPagedAsync(
                    100,
                    offset => spotify.Playlists.GetItems(playlistId,
                        new PlaylistGetItemsRequest { Limit = 100, Offset = offset }),
                    playlistTrack =>
                    {
                        var fullTrack = (FullTrack)playlistTrack.Track;
                        return new Track(fullTrack.Name, BuildArtists(fullTrack.Artists),
                            string.Empty, TimeSpan.Zero, Guid.Empty);
                    }
                );
            case var _ when argument.Contains("album"):
                var albumId = argument.Split("/").Last().Split("?").First();
                return await GetPagedAsync(50,
                    offset => spotify.Albums.GetTracks(
                        albumId,
                        new AlbumTracksRequest { Limit = 50, Offset = offset }),
                    simpleTrack => new Track(simpleTrack.Name, BuildArtists(simpleTrack.Artists), string.Empty,
                        TimeSpan.Zero, Guid.Empty)
                );
            case var _ when argument.Contains("artist"):
                var artistId = argument.Split("/").Last().Split("?").First();
                var topTracks = await spotify.Artists.GetTopTracks(artistId,
                    new ArtistsTopTracksRequest(CultureInfo.CurrentCulture.TwoLetterISOLanguageName));
                return topTracks.Tracks.Select(topTrack => new Track(topTrack.Name, BuildArtists(topTrack.Artists),
                    string.Empty, TimeSpan.Zero, Guid.Empty)).ToList();
        }

        return [];
    }

    private static string BuildArtists(IEnumerable<SimpleArtist> artists)
    {
        return string.Join(" & ", artists.Select(a => a.Name));
    }

    private static async Task<List<Track>> GetPagedAsync<T>(int limit, Func<int, Task<Paging<T>>> getTracksAsync,
        Func<T, Track> map)
    {
        var tracks = new List<Track>();
        var offset = 0;
        while (true)
        {
            var page = await getTracksAsync(offset);
            var mapped = page.Items?.Select(map).ToList() ?? [];
            tracks.AddRange(mapped);

            if (mapped.Count < limit)
            {
                break;
            }

            offset += limit;
        }

        return tracks;
    }
}
