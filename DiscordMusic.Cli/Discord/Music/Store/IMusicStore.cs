using System.IO.Abstractions;
using ByteSizeLib;

namespace DiscordMusic.Cli.Discord.Music.Store;

public interface IMusicStore
{
    Track GetOrAddTrack(TrackKey trackKey, Func<Guid, TrackKey, Track> factory);
    IFileInfo GetTrackFile(Track track);
    Track? FindTrack(string link);
    ByteSize GetSize();
    Task ClearAsync();
}
