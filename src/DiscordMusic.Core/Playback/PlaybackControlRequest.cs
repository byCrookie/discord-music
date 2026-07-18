namespace DiscordMusic.Core.Playback;

internal readonly record struct PlaybackControlRequest(
    PlaybackControlRequestType Type,
    TimeSpan Position
)
{
    public static PlaybackControlRequest None { get; } =
        new(PlaybackControlRequestType.None, TimeSpan.Zero);
}

internal enum PlaybackControlRequestType
{
    None,
    Skip,
    Stop,
    Seek,
}
