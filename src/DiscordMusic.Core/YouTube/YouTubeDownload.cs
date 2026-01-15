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
        var tempFile = $"{output}.tmp";
        var opusTempFile = $"{tempFile}.opus";

        try
        {
            logger.LogDebug("Downloading audio from YouTube for {Query} to {Output} with temporary file {TempFile}.",
                query, output.FullName, tempFile);

            var ytdlp = binaryLocator.LocateAndValidate(options.Value.Ytdlp, "yt-dlp");

            if (ytdlp.IsError)
            {
                logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToPrint());
                return Error.Unexpected(description: $"Failed to locate yt-dlp: {ytdlp.ToPrint()}");
            }

            var deno = binaryLocator.LocateAndValidate(options.Value.Deno, "deno");

            if (deno.IsError)
            {
                logger.LogError("Failed to locate deno: {Error}", deno.ToPrint());
                return Error.Unexpected(description: $"Failed to locate deno: {deno.ToPrint()}");
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

            YtdlpArgumentWriter.AppendRuntimeArguments(command, options.Value);

            logger.LogDebug("Start process {Ytdlp} with command {Command}.", ytdlp.Value.PathToFile, command);

            var ytdlpStartInfo = new ProcessStartInfo
            {
                FileName = ytdlp.Value.PathToFile,
                Arguments = command.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            AppendBinaryDirectoryToPath(ytdlpStartInfo, deno.Value);

            using var ytdlpProcess = Process.Start(ytdlpStartInfo);

            if (ytdlpProcess is null)
            {
                logger.LogError("Failed to start process yt-dlp with command {Command}.", command);
                return Error.Unexpected(description: $"Failed to start process yt-dlp with command {command}");
            }

            var ytdlpLines = new List<string>();
            var ytdlpErrors = new List<string>();

            ytdlpProcess.OutputDataReceived += (_, args) => ProcessOutput(args, ytdlpLines);
            ytdlpProcess.ErrorDataReceived += (_, args) => ProcessError(args, ytdlpErrors);

            ytdlpProcess.BeginOutputReadLine();
            ytdlpProcess.BeginErrorReadLine();

            await ytdlpProcess.WaitForExitAsync(ct);

            ytdlpProcess.CancelOutputRead();
            ytdlpProcess.CancelErrorRead();

            if (ytdlpProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, ytdlpErrors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ytdlpProcess.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            logger.LogDebug("YouTube download completed. Converting opus in {TempFile} to raw pcm in {Output}.",
                tempFile, output.FullName);

            var ffmpegArgs =
                $"-y -i \"{opusTempFile}\" -f s{AudioStream.BitsPerSample}le -ar {AudioStream.SampleRate} -ac {AudioStream.Channels} \"{output.FullName}\"";
            logger.LogTrace("Calling {Ffmpeg} with arguments {FfmpegArgs}", ffmpeg.Value.PathToFile, ffmpegArgs);
            using var ffmpegProcess = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ffmpeg.Value.PathToFile,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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

            var ffmpegLines = new List<string>();
            var ffmpegErrors = new List<string>();

            ffmpegProcess.OutputDataReceived += (_, args) => ProcessOutput(args, ffmpegLines);
            ffmpegProcess.ErrorDataReceived += (_, args) => ProcessError(args, ffmpegErrors);

            ffmpegProcess.BeginOutputReadLine();
            ffmpegProcess.BeginErrorReadLine();

            await ffmpegProcess.WaitForExitAsync(ct);

            ffmpegProcess.CancelOutputRead();
            ffmpegProcess.CancelErrorRead();

            if (ffmpegProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, ffmpegErrors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ffmpegProcess.ExitCode);
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

            if (fileSystem.File.Exists(tempFile))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", tempFile);
                fileSystem.File.Delete(tempFile);
            }
        }
    }

    private static void AppendBinaryDirectoryToPath(
        ProcessStartInfo startInfo,
        BinaryLocator.BinaryLocation location)
    {
        if (location.Type != BinaryLocator.LocationType.Resolved)
        {
            return;
        }

        var directory = location.PathToFolder;
        var pathKey = startInfo.Environment.Keys
            .FirstOrDefault(k => string.Equals(k, "PATH", StringComparison.OrdinalIgnoreCase))
            ?? "PATH";
        var existingPath = startInfo.Environment.TryGetValue(pathKey, out var current)
            ? current
            : Environment.GetEnvironmentVariable(pathKey) ?? string.Empty;

        existingPath ??= string.Empty;

        var segments = existingPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        startInfo.Environment[pathKey] = string.IsNullOrWhiteSpace(existingPath)
            ? directory
            : string.Join(Path.PathSeparator, new[] { directory, existingPath });
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

        var match = ErrorRegex().Match(e.Data);

        if (match.Success)
        {
            logger.LogError("{Message}", e.Data);
            errors.Add(match.Groups["Error"].Value);
        }
        else
        {
            logger.LogTrace("{Message}", e.Data);
        }
    }

    [GeneratedRegex("ERROR.*: (?<Error>.*)")]
    private static partial Regex ErrorRegex();
}
