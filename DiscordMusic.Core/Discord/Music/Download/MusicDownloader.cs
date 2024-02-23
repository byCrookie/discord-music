using System.Diagnostics;
using DiscordMusic.Core.Discord.Music.Store;
using DiscordMusic.Core.Discord.Options;
using DiscordMusic.Shared.Utils.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Discord.Music.Download;

internal class MusicDownloader(
    IMusicStore store,
    ILogger<MusicDownloader> logger,
    IOptions<DiscordOptions> discordOptions,
    IJsonSerializer jsonSerializer
) : IMusicDownloader
{
    public bool TryPrepare(string argument, out List<Track> tracks)
    {
        logger.LogDebug("Prepare tracks for {Argument}.", argument);

        var command =
            $"--default-search auto \"{argument}\" --no-download --flat-playlist --print \"%(.{{title,channel,duration,original_url}})j\"";

        logger.LogDebug("Run command {Command}.", command);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = discordOptions.Value.Ytdlp,
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process is not null)
        {
            process.ErrorDataReceived += LogStdError;
            process.BeginErrorReadLine();

            var json = process.StandardOutput.ReadToEnd();
            logger.LogTrace("{Json}", json);
            var lines = json.Split('\n');
            tracks = lines
                .Select(line => string.IsNullOrWhiteSpace(line) ? null : jsonSerializer.Deserialize<TrackInfo>(line))
                .Where(info => info is not null)
                .Select(info => info!)
                .Select(ToTrack)
                .ToList();
            logger.LogDebug("Prepared {Count} tracks.", tracks.Count);
            process.WaitForExit();
            return true;
        }

        logger.LogWarning("Failed to prepare tracks.");
        tracks = [];
        return false;
    }

    public bool TryDownload(Track track, out UpdatedTrack? updatedTrack)
    {
        if (string.IsNullOrWhiteSpace(track.Url))
        {
            if (TryPrepare($"{track.Title} - {track.Author}", out var preparedTracks))
            {
                track = preparedTracks.First();
            }
            else
            {
                updatedTrack = null;
                return false;
            }
        }

        logger.LogDebug("Download track {Track}.", track);
        var output = store.GetTrackFile(track);

        if (output.Exists)
        {
            logger.LogDebug("Track already exists at {File}.", output.FullName);
            updatedTrack = new UpdatedTrack(track, output);
            return true;
        }

        var command =
            $"--default-search auto \"{track.Url}\" -f \"bestaudio\" --ffmpeg-location \"{discordOptions.Value.Ffmpeg}\" --output \"{output}\" --no-playlist";

        logger.LogDebug("Run command {Command}.", command);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = discordOptions.Value.Ytdlp,
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process is not null)
        {
            process.OutputDataReceived += LogStdOut;
            process.ErrorDataReceived += LogStdError;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            updatedTrack = new UpdatedTrack(track, output);
            return true;
        }

        logger.LogWarning("Failed to download track.");
        updatedTrack = null;
        return false;
    }

    private Track ToTrack(TrackInfo info)
    {
        return store.GetOrAddTrack(new TrackKey(info.Title, info.Channel),
            (id, key) => new Track(key.Title, key.Author, info.Url, TimeSpan.FromSeconds(info.Duration), id));
    }

    private void LogStdOut(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogTrace("{Message}", e.Data);
    }

    private void LogStdError(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogWarning("{Message}", e.Data);
    }
}
