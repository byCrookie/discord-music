namespace DiscordMusic.Core.YouTube.Downloading;

public interface IYouTubeDownloadRequestProcessor
{
    Task ProcessAsync(YouTubeDownloadRequest request, CancellationToken cancellationToken);
}
