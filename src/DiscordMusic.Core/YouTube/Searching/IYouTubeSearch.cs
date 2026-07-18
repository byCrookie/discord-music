using ErrorOr;

namespace DiscordMusic.Core.YouTube.Searching;

public interface IYouTubeSearch
{
    public Task<ErrorOr<List<YouTubeTrack>>> SearchAsync(string query, CancellationToken ct);
}
