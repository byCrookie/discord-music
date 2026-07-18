using System.IO.Abstractions;
using DiscordMusic.Core.Tracks;

namespace DiscordMusic.Core.Storage;

public interface ITrackStorage
{
    public void SaveMetadata(Track track);
    public IFileInfo GetTrackPath(Track track, string extension);
}
