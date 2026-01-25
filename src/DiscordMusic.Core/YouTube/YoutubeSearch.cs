using System.Diagnostics;
using System.Text;
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
            logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToErrorContent());
            return Error.Unexpected(description: $"Failed to locate yt-dlp: {ytdlp.ToErrorContent()}");
        }

        var deno = binaryLocator.LocateAndValidate(youTubeOptions.Value.Deno, "deno");

        if (deno.IsError)
        {
            logger.LogError("Failed to locate deno: {Error}", deno.ToErrorContent());
            return Error.Unexpected(description: $"Failed to locate deno: {deno.ToErrorContent()}");
        }

        var command = new StringBuilder();
        command.Append($"--default-search auto \"{query}\"");
        command.Append(" --no-download");
        command.Append(" --flat-playlist");
        command.Append(" -j");

        YtdlpArgumentWriter.AppendRuntimeArguments(command, youTubeOptions.Value);

        var commandText = command.ToString();
        logger.LogTrace(
            "Start process {Ytdlp} with command {Command}.",
            ytdlp.Value.PathToFile,
            commandText
        );

        var startInfo = new ProcessStartInfo
        {
            FileName = ytdlp.Value.PathToFile,
            Arguments = commandText,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        AppendBinaryDirectoryToPath(startInfo, deno.Value);

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            logger.LogError(
                "Failed to start process {Ytdlp} with command {Command}.",
                ytdlp,
                commandText
            );
            return Error.Unexpected(
                description: $"Failed to start process {ytdlp} with command {commandText}"
            );
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
            return Error.Unexpected(
                description: $"Search on YouTube for {query} failed: {errorMessage}"
            );
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
}
