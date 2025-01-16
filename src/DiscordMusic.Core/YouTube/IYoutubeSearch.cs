using ErrorOr;

namespace DiscordMusic.Core.YouTube;

public interface IYoutubeSearch
{
    public Task<ErrorOr<List<YouTubeTrack>>> SearchAsync(string query, CancellationToken ct);
}
