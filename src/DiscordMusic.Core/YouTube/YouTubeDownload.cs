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
    public async Task<ErrorOr<Success>> DownloadAsync(
        string query,
        IFileInfo output,
        CancellationToken ct
    )
    {
        var tempFile = $"{output}.tmp";
        var opusTempFile = $"{tempFile}.opus";

        var baseMetadata = new Dictionary<string, object?>
        {
            [ErrorExtensions.MetadataKeys.Operation] = "youtube.download",
            ["query"] = query,
            ["output"] = output.FullName,
            ["tempFile"] = tempFile,
            ["opusTempFile"] = opusTempFile,
        };

        try
        {
            logger.LogDebug(
                "Downloading audio from YouTube. Query={Query} Output={Output}",
                query,
                output.FullName
            );

            var ytdlp = binaryLocator.LocateAndValidate(options.Value.Ytdlp, "yt-dlp");

            if (ytdlp.IsError)
            {
                logger.LogError(
                    "YouTube download dependency missing: yt-dlp. Query={Query} Error={Error}",
                    query,
                    ytdlp.ToErrorContent()
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DependencyMissing",
                        description: "YouTube playback isn't available right now."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("missingBinary", "yt-dlp");
            }

            var deno = binaryLocator.LocateAndValidate(options.Value.Deno, "deno");

            if (deno.IsError)
            {
                logger.LogError(
                    "YouTube download dependency missing: deno. Query={Query} Error={Error}",
                    query,
                    deno.ToErrorContent()
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DependencyMissing",
                        description: "YouTube playback isn't available right now."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("missingBinary", "deno");
            }

            var ffmpeg = binaryLocator.LocateAndValidate(options.Value.Ffmpeg, "ffmpeg");

            if (ffmpeg.IsError)
            {
                logger.LogError(
                    "YouTube download dependency missing: ffmpeg. Query={Query} Error={Error}",
                    query,
                    ffmpeg.ToErrorContent()
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DependencyMissing",
                        description: "YouTube playback isn't available right now."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("missingBinary", "ffmpeg");
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

            logger.LogDebug(
                "Start process {Ytdlp} with command {Command}.",
                ytdlp.Value.PathToFile,
                command
            );

            var ytdlpStartInfo = new ProcessStartInfo
            {
                FileName = ytdlp.Value.PathToFile,
                Arguments = command.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            AppendBinaryDirectoryToPath(ytdlpStartInfo, deno.Value);

            using var ytdlpProcess = Process.Start(ytdlpStartInfo);

            if (ytdlpProcess is null)
            {
                logger.LogError(
                    "Failed to start yt-dlp process. Query={Query} Output={Output} Args={Args}",
                    query,
                    output.FullName,
                    command.ToString()
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DownloadStartFailed",
                        description: "I couldn't start the YouTube download process."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("ytdlp", ytdlp.Value.PathToFile)
                    .WithMetadata("args", command.ToString());
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
                logger.LogError(
                    "YouTube download failed. ExitCode={ExitCode} Query={Query} Output={Output} Error={Error}",
                    ytdlpProcess.ExitCode,
                    query,
                    output.FullName,
                    errorMessage
                );
                return Error
                    .Unexpected(
                        code: "YouTube.DownloadFailed",
                        description: "Downloading from YouTube failed."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("exitCode", ytdlpProcess.ExitCode)
                    .WithMetadata("stderr", errorMessage)
                    .WithMetadata("ytdlp", ytdlp.Value.PathToFile);
            }

            logger.LogDebug(
                "YouTube download completed. Converting opus in {TempFile} to raw pcm in {Output}.",
                tempFile,
                output.FullName
            );

            var ffmpegArgs =
                $"-y -i \"{opusTempFile}\" -f s{Pcm16Bytes.BitsPerSample}le -ar {Pcm16Bytes.SampleRate} -ac {Pcm16Bytes.Channels} \"{output.FullName}\"";
            logger.LogTrace(
                "Calling {Ffmpeg} with arguments {FfmpegArgs}",
                ffmpeg.Value.PathToFile,
                ffmpegArgs
            );
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
                    "Failed to start ffmpeg process. Query={Query} Output={Output} Args={Args}",
                    query,
                    output.FullName,
                    ffmpegArgs
                );

                return Error
                    .Unexpected(
                        code: "Audio.ConvertStartFailed",
                        description: "I couldn't start the audio converter (ffmpeg)."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("ffmpeg", ffmpeg.Value.PathToFile)
                    .WithMetadata("ffmpegArgs", ffmpegArgs);
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
                logger.LogError(
                    "Audio conversion failed. ExitCode={ExitCode} Query={Query} Output={Output} Error={Error}",
                    ffmpegProcess.ExitCode,
                    query,
                    output.FullName,
                    errorMessage
                );

                return Error
                    .Unexpected(
                        code: "Audio.ConvertFailed",
                        description: "Converting the downloaded audio failed."
                    )
                    .WithMetadata(baseMetadata)
                    .WithMetadata("exitCode", ffmpegProcess.ExitCode)
                    .WithMetadata("stderr", errorMessage)
                    .WithMetadata("ffmpeg", ffmpeg.Value.PathToFile)
                    .WithMetadata("ffmpegArgs", ffmpegArgs);
            }

            logger.LogInformation(
                "YouTube download succeeded. Query={Query} Output={Output}",
                query,
                output.FullName
            );

            return Result.Success;
        }
        catch (Exception e)
        {
            logger.LogError(
                e,
                "YouTube download crashed. Query={Query} Output={Output}",
                query,
                output.FullName
            );

            return Error
                .Unexpected(
                    code: "YouTube.DownloadCrashed",
                    description: "An error occurred while downloading from YouTube."
                )
                .WithMetadata(baseMetadata)
                .WithException(e);
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
        BinaryLocator.BinaryLocation location
    )
    {
        if (location.Type != BinaryLocator.LocationType.Resolved)
        {
            return;
        }

        var directory = location.PathToFolder;
        var pathKey =
            startInfo.Environment.Keys.FirstOrDefault(k =>
                string.Equals(k, "PATH", StringComparison.OrdinalIgnoreCase)
            ) ?? "PATH";
        var existingPath = startInfo.Environment.TryGetValue(pathKey, out var current)
            ? current
            : Environment.GetEnvironmentVariable(pathKey) ?? string.Empty;

        existingPath ??= string.Empty;

        var segments = existingPath.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

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
