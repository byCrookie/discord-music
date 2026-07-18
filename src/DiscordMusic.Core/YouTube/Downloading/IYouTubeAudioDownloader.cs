using System.IO.Abstractions;
using ErrorOr;

namespace DiscordMusic.Core.YouTube.Downloading;

internal interface IYouTubeAudioDownloader
{
    Task<ErrorOr<IFileInfo>> DownloadAsync(
        string query,
        IFileInfo outputBase,
        CancellationToken ct
    );
}
