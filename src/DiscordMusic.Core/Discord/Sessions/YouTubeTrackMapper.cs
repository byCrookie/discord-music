using DiscordMusic.Core.YouTube;

namespace DiscordMusic.Core.Discord.Sessions;

internal static class YouTubeTrackMapper
{
    internal static Track ToTrack(YouTubeTrack yt)
    {
        return new Track(yt.Title, yt.Channel, yt.Url, TimeSpan.FromSeconds(yt.Duration ?? 0));
    }
}
