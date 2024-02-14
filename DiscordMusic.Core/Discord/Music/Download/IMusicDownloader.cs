using System.IO.Abstractions;

namespace DiscordMusic.Core.Discord.Music.Download;

internal interface IMusicDownloader
{
    public bool TryPrepare(string argument, out List<Track> tracks);
    public bool TryDownload(Track track, out IFileInfo? file);
}
