using ErrorOr;

namespace DiscordMusic.Core.Spotify;

public interface ISpotifySearch
{
    bool IsSpotifyQuery(string query);
    Task<ErrorOr<List<SpotifyTrack>>> SearchAsync(string query, CancellationToken ct);
}
