namespace DiscordMusic.Core.Playback;

internal readonly record struct PlaybackCommandResult(bool IsSuccess, string Message)
{
    public static PlaybackCommandResult Success(string message)
    {
        return new PlaybackCommandResult(true, message);
    }

    public static PlaybackCommandResult Failure(string message)
    {
        return new PlaybackCommandResult(false, message);
    }
}
