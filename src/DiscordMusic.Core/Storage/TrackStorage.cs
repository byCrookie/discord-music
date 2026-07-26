using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils.Json;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Storage;

internal class TrackStorage(
    IFileSystem fileSystem,
    IStoragePathProvider storagePathProvider,
    IJsonSerializer jsonSerializer,
    ILogger<TrackStorage> logger
) : ITrackStorage
{
    private const string SubDirectory = "tracks";

    public void SaveMetadata(Track track)
    {
        using var activity = DiscordMusicObservability.StartActivity("storage.track.metadata.save");
        DiscordMusicObservability.SetTag(activity, "music.track.id", track.Id);
        var result = "completed";

        try
        {
            if (!fileSystem.Directory.Exists(TracksPath))
            {
                logger.LogInformation(
                    "Tracks directory {TracksPath} does not exist. Creating it.",
                    TracksPath
                );
                fileSystem.Directory.CreateDirectory(TracksPath);
            }

            var metadataFile = fileSystem.FileInfo.New(
                fileSystem.Path.Combine(TracksPath, $"{track.Id}.json")
            );
            var trackJson = jsonSerializer.Serialize(track);
            fileSystem.File.WriteAllText(metadataFile.FullName, trackJson);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            result = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            RecordStorageOperation("track_metadata_save", result);
        }
    }

    public IFileInfo GetTrackPath(Track track, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException(
                "Track file extension must be provided.",
                nameof(extension)
            );
        }

        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return fileSystem.FileInfo.New(
            fileSystem.Path.Combine(TracksPath, $"{track.Id}{normalizedExtension}")
        );
    }

    public bool IsTrackCached(Track track, string extension)
    {
        using var activity = DiscordMusicObservability.StartActivity("storage.track.cache.lookup");
        DiscordMusicObservability.SetTag(activity, "music.track.id", track.Id);
        DiscordMusicObservability.SetTag(activity, "file.extension", extension);
        var exists = GetTrackPath(track, extension).Exists;
        DiscordMusicObservability.SetTag(activity, "result", exists ? "hit" : "miss");
        activity?.SetStatus(ActivityStatusCode.Ok, exists ? "hit" : "miss");
        RecordStorageOperation("track_cache_lookup", exists ? "hit" : "miss");
        return exists;
    }

    private static void RecordStorageOperation(string operation, string result)
    {
        DiscordMusicObservability.StorageOperations.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", result)
        );
    }

    private string TracksPath =>
        fileSystem.Path.Combine(storagePathProvider.StorageDirectory().FullName, SubDirectory);
}
