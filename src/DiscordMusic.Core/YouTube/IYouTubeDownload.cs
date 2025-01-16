using System.IO.Abstractions;
using ErrorOr;

namespace DiscordMusic.Core.YouTube;

public interface IYouTubeDownload
{
    public Task<ErrorOr<Success>> DownloadAsync(string query, IFileInfo output, CancellationToken ct);
}
