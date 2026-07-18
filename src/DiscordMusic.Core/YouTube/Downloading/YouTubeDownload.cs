using System.IO.Abstractions;
using DiscordMusic.Core.YouTube.Conversion;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Downloading;

internal sealed class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IFileSystem fileSystem,
    IYouTubeAudioDownloader audioDownloader,
    IAudioConverter audioConverter
) : IYouTubeDownload
{
    public async Task<ErrorOr<Success>> DownloadAsync(
        string query,
        IFileInfo output,
        CancellationToken ct
    )
    {
        var tempFile = fileSystem.FileInfo.New($"{output.FullName}.tmp");
        IFileInfo? downloadedFile = null;

        try
        {
            var download = await audioDownloader.DownloadAsync(query, tempFile, ct);
            if (download.IsError)
            {
                return download.Errors;
            }

            downloadedFile = download.Value;
            var conversion = await audioConverter.ConvertToPcmAsync(downloadedFile, output, ct);
            if (conversion.IsError)
            {
                return conversion.Errors;
            }

            logger.LogInformation(
                "YouTube download succeeded. Query={Query} Output={Output}",
                query,
                output.FullName
            );
            return Result.Success;
        }
        finally
        {
            if (fileSystem.File.Exists(tempFile.FullName))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", tempFile.FullName);
                fileSystem.File.Delete(tempFile.FullName);
            }

            if (downloadedFile is not null && fileSystem.File.Exists(downloadedFile.FullName))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", downloadedFile.FullName);
                fileSystem.File.Delete(downloadedFile.FullName);
            }
        }
    }
}
