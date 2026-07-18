namespace DiscordMusic.Core.YouTube.Searching;

public interface IYouTubeSearchRequestProcessor
{
    Task ProcessAsync(YouTubeSearchRequest request, CancellationToken cancellationToken);
}
