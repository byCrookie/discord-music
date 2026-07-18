using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal static class AudioBarRenderer
{
    private const int BarWidth = 24;

    public static string Render(
        PlaybackSession.PlaybackSnapshot snapshot,
        TimeSpan? positionOverride = null,
        AudioBarRenderMode mode = AudioBarRenderMode.Standalone
    )
    {
        if (snapshot.CurrentTrack is not { } track)
        {
            return "Nothing is currently playing.";
        }

        var state = snapshot.State == PlaybackState.Paused ? "Paused" : "Playing";
        var position = positionOverride ?? snapshot.Position;
        var formattedTrack = DiscordResponses.FormatTrack(track, includeDuration: false);

        if (mode == AudioBarRenderMode.Inline)
        {
            var label = snapshot.State == PlaybackState.Paused ? "Paused" : "Now playing";
            return $"""
                **{label}:** {formattedTrack}
                {RenderProgress(track, position)}
                """;
        }

        return $"""
            ### {state}
            {formattedTrack}
            {RenderProgress(track, position)}
            """;
    }

    private static string RenderProgress(Track track, TimeSpan position)
    {
        if (track.Duration <= TimeSpan.Zero)
        {
            return $"`{position.HumanizeSecond()} elapsed`";
        }

        var ratio = Math.Clamp(position.TotalMilliseconds / track.Duration.TotalMilliseconds, 0, 1);
        var filled = Math.Clamp((int)Math.Round(ratio * BarWidth), 0, BarWidth);
        var bar = new string('#', filled) + new string('-', BarWidth - filled);

        return $"`{bar}` {position.HumanizeSecond()} / {track.Duration.HumanizeSecond()}";
    }
}
