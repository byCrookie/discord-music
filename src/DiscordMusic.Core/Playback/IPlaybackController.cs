namespace DiscordMusic.Core.Playback;

internal interface IPlaybackController
{
    PlaybackCommandResult Pause(ulong guildId, PlaybackSession session);
    PlaybackCommandResult Resume(ulong guildId, PlaybackSession session);
    PlaybackCommandResult Seek(ulong guildId, PlaybackSession session, TimeSpan position);
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
