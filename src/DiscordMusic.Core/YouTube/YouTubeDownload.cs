using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Audio;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube;

internal partial class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IFileSystem fileSystem
) : IYouTubeDownload
{
    public async Task<ErrorOr<Success>> DownloadAsync(string query, IFileInfo output, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var opusTempFile = $"{tempFile}.opus";

        try
        {
            var command =
                $"--default-search auto \"{query}\" -f \"bestaudio\" --extract-audio --audio-format opus --audio-quality 0 --output \"{tempFile}\" --no-playlist";

            logger.LogDebug("Start process yt-dlp with command {Command}.", command);

            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            );

            if (process is null)
            {
                logger.LogError("Failed to start process yt-dlp with command {Command}.", command);
                return Error.Unexpected(description: $"Failed to start process yt-dlp with command {command}");
            }

            var lines = new List<string>();
            var errors = new List<string>();

            process.OutputDataReceived += (_, args) => ProcessOutput(args, lines);
            process.ErrorDataReceived += (_, args) => ProcessError(args, errors);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", process.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            var ffmpegArgs =
                $"-i \"{opusTempFile}\" -f s{AudioStream.BitsPerSample}le -ar {AudioStream.SampleRate} -ac {AudioStream.Channels} {output.FullName}";
            logger.LogTrace("Calling ffmpeg with arguments {FfmpegArgs}", ffmpegArgs);
            using var ffmpeg = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (ffmpeg is null)
            {
                logger.LogError(
                    "Failed to start process ffmpeg with arguments {FfmpegArgs}.",
                    ffmpegArgs
                );
                return Error.Unexpected(
                    description: $"Failed to start process ffmpeg with arguments {ffmpegArgs}"
                );
            }

            await ffmpeg.WaitForExitAsync(ct);

            if (ffmpeg.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", process.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            logger.LogDebug("YouTube download completed. Audio file saved at {Output}.", output.FullName);
            return Result.Success;
        }
        finally
        {
            if (fileSystem.File.Exists(opusTempFile))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", opusTempFile);
                fileSystem.File.Delete(opusTempFile);
            }
        }
    }

    private void ProcessOutput(DataReceivedEventArgs e, List<string> lines)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogTrace("{Message}", e.Data);
        lines.Add(e.Data);
    }

    private void ProcessError(DataReceivedEventArgs e, List<string> errors)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogWarning("{Message}", e.Data);
        var match = ErrorRegex().Match(e.Data);

        if (match.Success)
        {
            errors.Add(match.Groups["Error"].Value);
        }
    }

    [GeneratedRegex("ERROR.*: (?<Error>.*)")]
    private static partial Regex ErrorRegex();
}
