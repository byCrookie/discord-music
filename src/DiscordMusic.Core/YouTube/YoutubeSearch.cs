using System.Diagnostics;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube;

internal partial class YoutubeSearch(
    ILogger<YoutubeSearch> logger,
    IJsonSerializer jsonSerializer
) : IYoutubeSearch
{
    public async Task<ErrorOr<List<YouTubeTrack>>> SearchAsync(string query, CancellationToken ct)
    {
        logger.LogDebug("Searching YouTube for {Query}.", query);

        var command = $"--default-search auto \"{query}\" --no-download --flat-playlist -j";

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

        logger.LogWarning("{Message}", e.Data);
        var match = ErrorRegex().Match(e.Data);

        if (match.Success)
        {
            errors.Add(match.Groups["Error"].Value);
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
