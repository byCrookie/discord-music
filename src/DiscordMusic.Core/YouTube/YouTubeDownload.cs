using System.Diagnostics;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

internal partial class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IOptions<YouTubeOptions> youTubeOptions,
    IFileSystem fileSystem
) : IYouTubeDownload
{
    public async Task<ErrorOr<Success>> DownloadAsync(string query, IFileInfo output, CancellationToken ct)
    {
        var command =
            $"--default-search auto \"{query}\" -f \"bestaudio\" --ffmpeg-location \"{youTubeOptions.Value.Ffmpeg}\" --extract-audio --audio-format opus --audio-quality 0 --output \"{output}\" --no-playlist";
        var ytdlp = youTubeOptions.Value.Ytdlp;

        logger.LogDebug("Start process {Ytdlp} with command {Command}.", ytdlp, command);

        var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = ytdlp,
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        );

        if (process is null)
        {
            logger.LogError("Failed to start process {Ytdlp} with command {Command}.", ytdlp, command);
            return Error.Unexpected(description: $"Failed to start process {ytdlp} with command {command}");
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
            return Error.Unexpected(description: $"Download from YouTube failed: {errorMessage}");
        }

        fileSystem.File.Move($"{output.FullName}.opus", output.FullName);

        logger.LogDebug("YouTube download completed. Audio file saved at {Output}.", output.FullName);
        return new Success();
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
