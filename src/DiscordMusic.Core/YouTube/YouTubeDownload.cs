using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

internal partial class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IOptions<YouTubeOptions> options,
    IFileSystem fileSystem,
    BinaryLocator binaryLocator
) : IYouTubeDownload
{
    public async Task<ErrorOr<Success>> DownloadAsync(string query, IFileInfo output, CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var opusTempFile = $"{tempFile}.opus";

        try
        {
            var ytdlp = binaryLocator.LocateAndValidate(options.Value.Ytdlp, "yt-dlp");
            
            if (ytdlp.IsError)
            {
                logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToPrint());
                return Error.Unexpected(description: $"Failed to locate yt-dlp: {ytdlp.ToPrint()}");
            }
            
            var ffmpeg = binaryLocator.LocateAndValidate(options.Value.Ffmpeg, "ffmpeg");
            
            if (ffmpeg.IsError)
            {
                logger.LogError("Failed to locate ffmpeg: {Error}", ffmpeg.ToPrint());
                return Error.Unexpected(description: $"Failed to locate ffmpeg: {ffmpeg.ToPrint()}");
            }

            var command = new StringBuilder();
            command.Append($"--default-search auto \"{query}\"");
            command.Append(" -f \"bestaudio\"");
            command.Append(" --extract-audio");
            
            if (ffmpeg.Value.Type == BinaryLocator.LocationType.Resolved)
            {
                command.Append($" --ffmpeg-location \"{ffmpeg.Value.PathToFolder}\"");
            }
            
            command.Append(" --audio-format opus");
            command.Append(" --audio-quality 0");
            command.Append($" --output \"{tempFile}\"");
            command.Append(" --no-playlist");
            
            logger.LogDebug("Start process yt-dlp with command {Command}.", command);

            using var ytdlpProcess = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ytdlp.Value.PathToFile,
                    Arguments = command.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            );

            if (ytdlpProcess is null)
            {
                logger.LogError("Failed to start process yt-dlp with command {Command}.", command);
                return Error.Unexpected(description: $"Failed to start process yt-dlp with command {command}");
            }

            var lines = new List<string>();
            var errors = new List<string>();

            ytdlpProcess.OutputDataReceived += (_, args) => ProcessOutput(args, lines);
            ytdlpProcess.ErrorDataReceived += (_, args) => ProcessError(args, errors);

            ytdlpProcess.BeginOutputReadLine();
            ytdlpProcess.BeginErrorReadLine();

            await ytdlpProcess.WaitForExitAsync(ct);

            if (ytdlpProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ytdlpProcess.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            var ffmpegArgs =
                $"-i \"{opusTempFile}\" -f s{AudioStream.BitsPerSample}le -ar {AudioStream.SampleRate} -ac {AudioStream.Channels} {output.FullName}";
            logger.LogTrace("Calling ffmpeg with arguments {FfmpegArgs}", ffmpegArgs);
            using var ffmpegProcess = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ffmpeg.Value.PathToFile,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (ffmpegProcess is null)
            {
                logger.LogError(
                    "Failed to start process ffmpeg with arguments {FfmpegArgs}.",
                    ffmpegArgs
                );
                return Error.Unexpected(
                    description: $"Failed to start process ffmpeg with arguments {ffmpegArgs}"
                );
            }

            await ffmpegProcess.WaitForExitAsync(ct);

            if (ffmpegProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ytdlpProcess.ExitCode);
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
