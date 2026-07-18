namespace DiscordMusic.Core.YouTube.Downloading;

internal interface IYouTubeDownloadScheduler
{
    Task EnsureNextTrackQueuedAsync(ulong guildId, CancellationToken cancellationToken);
}
