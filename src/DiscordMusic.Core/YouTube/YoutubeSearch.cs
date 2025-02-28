using System.Diagnostics;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

internal partial class YoutubeSearch(
    ILogger<YoutubeSearch> logger,
    IOptions<YouTubeOptions> youTubeOptions,
    IJsonSerializer jsonSerializer,
    BinaryLocator binaryLocator
) : IYoutubeSearch
{
    public async Task<ErrorOr<List<YouTubeTrack>>> SearchAsync(string query, CancellationToken ct)
    {
        logger.LogDebug("Searching YouTube for {Query}.", query);

        var ytdlp = binaryLocator.LocateAndValidate(youTubeOptions.Value.Ytdlp, "yt-dlp");

        if (ytdlp.IsError)
        {
            logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToPrint());
            return Error.Unexpected(description: $"Failed to locate yt-dlp: {ytdlp.ToPrint()}");
        }

        var command =
            $"--default-search auto \"{query}\" --no-download --flat-playlist -j --user-agent \"Firefox 134.0, Linux\"";
        logger.LogTrace("Start process {Ytdlp} with command {Command}.", ytdlp.Value.PathToFile, command);

        using var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = ytdlp.Value.PathToFile,
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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

        process.CancelOutputRead();
        process.CancelErrorRead();

        if (process.ExitCode != 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            logger.LogError("YouTube search failed with exit code {ExitCode}", process.ExitCode);
            return Error.Unexpected(description: $"Search on YouTube for {query} failed: {errorMessage}");
        }

        var tracks = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(jsonSerializer.Deserialize<YouTubeTrack>)
            .DistinctBy(e => e.Url)
            .ToList();

        logger.LogDebug("Found {Count} tracks on YouTube for {Query}.", tracks.Count, query);
        return tracks.Count != 0 ? tracks : [];
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

    private void ProcessOutput(DataReceivedEventArgs e, List<string> lines)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogTrace("{Message}", e.Data);
        lines.Add(e.Data);
    }

    [GeneratedRegex("ERROR.*: (?<Error>.*)")]
    private static partial Regex ErrorRegex();
}
