using System.IO.Abstractions;
using ByteSizeLib;

namespace DiscordMusic.Core.Discord.Music.Store;

public interface IMusicStore
{
    Track GetOrAddTrack(TrackKey trackKey, Func<Guid, TrackKey, Track> factory);
    IFileInfo GetTrackFile(Track track);
    ByteSize GetSize();
    Task ClearAsync();
}
