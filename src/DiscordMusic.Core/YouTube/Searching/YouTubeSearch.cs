using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Utils.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube.Searching;

internal partial class YouTubeSearch(
    ILogger<YouTubeSearch> logger,
    IOptions<YouTubeOptions> youTubeOptions,
    IJsonSerializer jsonSerializer,
    YouTubeToolLocations toolLocations,
    IEnvironmentVariables environmentVariables,
    IFileSystem fileSystem
) : IYouTubeSearch
{
    public async Task<ErrorOr<List<YouTubeTrack>>> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            logger.LogDebug("Searching YouTube. Query={Query}", query);
            var loadedLocations = toolLocations.Value;

            var command = new StringBuilder();
            command.Append($"--default-search auto \"{query}\"");
            command.Append(" --no-download");
            command.Append(" --flat-playlist");
            command.Append(" -j");

            YtdlpArgumentWriter.AppendRuntimeArguments(command, youTubeOptions.Value);

            var commandText = command.ToString();
            logger.LogTrace(
                "Starting yt-dlp for YouTube search. Ytdlp={Ytdlp} Args={Args} Query={Query}",
                loadedLocations.Ytdlp.PathToFile,
                commandText,
                query
            );

            var startInfo = new ProcessStartInfo
            {
                FileName = loadedLocations.Ytdlp.PathToFile,
                Arguments = commandText,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            AppendBinaryDirectoryToPath(
                startInfo,
                loadedLocations.Deno,
                environmentVariables,
                fileSystem
            );

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                logger.LogError(
                    "Failed to start yt-dlp process for YouTube search. Ytdlp={Ytdlp} Args={Args} Query={Query}",
                    loadedLocations.Ytdlp.PathToFile,
                    commandText,
                    query
                );

                return Error
                    .Unexpected(
                        code: "YouTube.SearchStartFailed",
                        description: "I couldn't start the YouTube search process."
                    )
                    .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "youtube.search")
                    .WithMetadata("query", query)
                    .WithMetadata("ytdlp", loadedLocations.Ytdlp.PathToFile)
                    .WithMetadata("args", commandText);
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

                logger.LogError(
                    "YouTube search failed. ExitCode={ExitCode} Query={Query} Error={Error}",
                    process.ExitCode,
                    query,
                    errorMessage
                );

                return Error
                    .Unexpected(code: "YouTube.SearchFailed", description: "YouTube search failed.")
                    .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "youtube.search")
                    .WithMetadata("query", query)
                    .WithMetadata("exitCode", process.ExitCode)
                    .WithMetadata("stderr", errorMessage)
                    .WithMetadata("ytdlp", loadedLocations.Ytdlp.PathToFile);
            }

            var tracks = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(jsonSerializer.Deserialize<YouTubeTrack>)
                .DistinctBy(e => e.Url)
                .ToList();

            logger.LogDebug("Found {Count} tracks on YouTube for {Query}.", tracks.Count, query);
            return tracks.Count != 0 ? tracks : [];
        }
        catch (Exception e)
        {
            logger.LogError(e, "YouTube search crashed. Query={Query}", query);

            return Error
                .Unexpected(
                    code: "YouTube.SearchCrashed",
                    description: "YouTube search failed unexpectedly."
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "youtube.search")
                .WithMetadata("query", query)
                .WithException(e);
        }
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
        BinaryLocator.BinaryLocation location,
        IEnvironmentVariables environmentVariables,
        IFileSystem fileSystem
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
            : environmentVariables.GetVariable(pathKey) ?? string.Empty;

        existingPath ??= string.Empty;

        var segments = existingPath.Split(
            fileSystem.Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

        if (segments.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        startInfo.Environment[pathKey] = string.IsNullOrWhiteSpace(existingPath)
            ? directory
            : string.Join(fileSystem.Path.PathSeparator, new[] { directory, existingPath });
    }
}
