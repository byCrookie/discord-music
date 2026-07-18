namespace DiscordMusic.Core.Playback;

internal interface IPlaybackController
{
    PlaybackCommandResult Pause(PlaybackSession session);
    PlaybackCommandResult Resume(PlaybackSession session);
    PlaybackCommandResult Seek(PlaybackSession session, TimeSpan position);
    Task<PlaybackCommandResult> SkipAsync(
        ulong guildId,
        PlaybackSession session,
        CancellationToken cancellationToken
    );
    Task<PlaybackCommandResult> SkipToAsync(
        ulong guildId,
        PlaybackSession session,
        int queueIndex,
        CancellationToken cancellationToken
    );
    PlaybackCommandResult Stop(ulong guildId, PlaybackSession session);
}
