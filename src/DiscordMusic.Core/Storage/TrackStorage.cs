using System.IO.Abstractions;
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

    private string TracksPath =>
        fileSystem.Path.Combine(storagePathProvider.StorageDirectory().FullName, SubDirectory);
}
