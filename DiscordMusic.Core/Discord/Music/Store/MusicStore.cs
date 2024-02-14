using System.Collections.Concurrent;
using System.IO.Abstractions;
using ByteSizeLib;
using DiscordMusic.Core.Data;
using DiscordMusic.Shared.Utils.Json;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DiscordMusic.Core.Discord.Music.Store;

internal class MusicStore : IMusicStore
{
    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly Lazy<IDirectoryInfo> _location;
    private readonly ILogger<MusicStore> _logger;
    private readonly Lazy<ConcurrentDictionary<TrackKey, Track>> _populate;

    private ConcurrentDictionary<TrackKey, Track>? _cache;

    public MusicStore(
        IFileSystem fileSystem,
        IDataStore dataStore,
        ILogger<MusicStore> logger,
        IJsonSerializer jsonSerializer)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _jsonSerializer = jsonSerializer;

        _location = new Lazy<IDirectoryInfo>(() => dataStore.Require("tracks"));
        _populate = new Lazy<ConcurrentDictionary<TrackKey, Track>>(() =>
        {
            _logger.LogDebug("Populating music store.");
            var tracks = new ConcurrentDictionary<TrackKey, Track>();
            foreach (var file in _location.Value.GetFiles().Where(f => f.Extension == ".json"))
            {
                _logger.LogTrace("Reading file {File}.", file.FullName);
                var content = _fileSystem.File.ReadAllText(file.FullName);
                _logger.LogTrace("Deserializing content {Content}.", content);
                var track = JsonSerializer.Deserialize<Track>(content)!;
                _logger.LogTrace("Adding track {Track}", track);
                tracks.TryAdd(new TrackKey(track.Title, track.Author), track);
            }

            _logger.LogDebug("Populated music store with {Count} tracks.", tracks.Count);
            return tracks;
        });
    }

    public Track GetOrAddTrack(TrackKey trackKey, Func<Guid, TrackKey, Track> factory)
    {
        _cache ??= _populate.Value;
        _logger.LogTrace("Get or add track for {TrackKey}.", trackKey);
        return _cache.GetOrAdd(trackKey, key =>
        {
            var track = factory(Guid.NewGuid(), key);
            _logger.LogTrace("Add track {Track}.", track);
            var path = GetJsonPath(track);
            _logger.LogTrace("Write track info {Track} to {Path}.", track, path.FullName);
            _fileSystem.File.WriteAllText(path.FullName, _jsonSerializer.Serialize(track));
            return track;
        });
    }

    public IFileInfo GetTrackFile(Track track)
    {
        _cache ??= _populate.Value;
        _logger.LogTrace("Get path for {Track}.", track);
        return GetPath(_cache.GetOrAdd(new TrackKey(track.Title, track.Author), _ =>
        {
            _logger.LogTrace("Add track {Track}.", track);
            var path = GetJsonPath(track);
            _logger.LogTrace("Write track info {Track} to {Path}.", track, path.FullName);
            _fileSystem.File.WriteAllText(path.FullName, _jsonSerializer.Serialize(track));
            return track;
        }));
    }

    public ByteSize GetSize()
    {
        _logger.LogTrace("Get music store size.");
        var location = _location.Value;
        var size = location.GetFiles().Sum(f => f.Length);
        var byteSize = ByteSize.FromBytes(size);
        _logger.LogTrace("Music store size is {Size}.", byteSize);
        return byteSize;
    }

    public Task ClearAsync()
    {
        _logger.LogDebug("Clear music store.");
        var location = _location.Value;
        foreach (var file in location.GetFiles())
        {
            _logger.LogTrace("Delete file {File}.", file.FullName);
            file.Delete();
        }

        return Task.CompletedTask;
    }

    private IFileInfo GetPath(Track track)
    {
        var path = _fileSystem.Path.Combine(_location.Value.FullName, $"{track.Id}.opus");
        _logger.LogTrace("Audio file path for {Track} is {Path}.", track, path);
        return _fileSystem.FileInfo.New(path);
    }

    private IFileInfo GetJsonPath(Track track)
    {
        var path = _fileSystem.Path.Combine(_location.Value.FullName, $"{track.Id}.json");
        _logger.LogTrace("Metadata path for {Track} is {Path}.", track, path);
        return _fileSystem.FileInfo.New(path);
    }
}
