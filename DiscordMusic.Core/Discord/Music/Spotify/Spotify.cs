using System.Globalization;
using DiscordMusic.Core.Discord.Options.Spotify;
using Microsoft.Extensions.Options;
using SpotifyAPI.Web;

namespace DiscordMusic.Core.Discord.Music.Spotify;

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
                return
                [
                    new Track(track.Name, string.Join("&", track.Artists.Select(a => a.Name)), string.Empty,
                        TimeSpan.Zero, Guid.Empty)
                ];
            case var _ when argument.Contains("playlist"):
                var playlistId = argument.Split("/").Last().Split("?").First();
                var playlistTracks = await spotify.Playlists.GetItems(playlistId);
                return (from playlistTrack in playlistTracks.Items?.Select(i => (FullTrack)i.Track) ??
                                              Enumerable.Empty<FullTrack>()
                    select new Track(
                        playlistTrack.Name, string.Join("&", playlistTrack.Artists.Select(a => a.Name)), string.Empty,
                        TimeSpan.Zero, Guid.Empty)).ToList();
            case var _ when argument.Contains("album"):
                var albumId = argument.Split("/").Last().Split("?").First();
                var albumTracks = await spotify.Albums.GetTracks(albumId);
                return albumTracks.Items
                    ?.Select(albumTrack => new Track(albumTrack.Name,
                        string.Join("&", albumTrack.Artists.Select(a => a.Name)), string.Empty,
                        TimeSpan.Zero, Guid.Empty))
                    .ToList() ?? [];
            case var _ when argument.Contains("artist"):
                var artistId = argument.Split("/").Last().Split("?").First();
                var topTracks = await spotify.Artists.GetTopTracks(artistId,
                    new ArtistsTopTracksRequest(CultureInfo.CurrentCulture.TwoLetterISOLanguageName));
                return topTracks.Tracks.Select(albumTrack => new Track(albumTrack.Name,
                        string.Join("&", albumTrack.Artists.Select(a => a.Name)), string.Empty,
                        TimeSpan.Zero, Guid.Empty))
                    .ToList();
        }

        return [];
    }
}
