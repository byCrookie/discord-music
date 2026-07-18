using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Utils;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord.Commands;

internal sealed class AudioBarComponentModule(
    VoiceConnectionRegistry voiceConnections,
    PlaybackService playbackService,
    IPlaybackController playbackController
) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(AudioBarComponents.Rewind30)]
    public Task<InteractionMessageProperties> Rewind30Async()
    {
        return SeekRelativeAsync(TimeSpan.FromSeconds(-30));
    }

    [ComponentInteraction(AudioBarComponents.Rewind10)]
    public Task<InteractionMessageProperties> Rewind10Async()
    {
        return SeekRelativeAsync(TimeSpan.FromSeconds(-10));
    }

    [ComponentInteraction(AudioBarComponents.PlayPause)]
    public InteractionMessageProperties PlayPause()
    {
        if (!TryGetSession(out var session, out var error))
        {
            return error;
        }

        var snapshot = session.Snapshot();
        var result =
            snapshot.State == PlaybackState.Paused
                ? playbackController.Resume(session)
                : playbackController.Pause(session);

        return DiscordResponses.PlaybackFeedback(result, session);
    }

    [ComponentInteraction(AudioBarComponents.Forward10)]
    public Task<InteractionMessageProperties> Forward10Async()
    {
        return SeekRelativeAsync(TimeSpan.FromSeconds(10));
    }

    [ComponentInteraction(AudioBarComponents.Forward30)]
    public Task<InteractionMessageProperties> Forward30Async()
    {
        return SeekRelativeAsync(TimeSpan.FromSeconds(30));
    }

    private Task<InteractionMessageProperties> SeekRelativeAsync(TimeSpan offset)
    {
        if (!TryGetSession(out var session, out var error))
        {
            return Task.FromResult(error);
        }

        var snapshot = session.Snapshot();
        if (snapshot.CurrentTrack is null)
        {
            return Task.FromResult(DiscordResponses.Ephemeral("Nothing is currently playing."));
        }

        var position = snapshot.Position + offset;
        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }

        if (
            snapshot.CurrentTrack.Duration > TimeSpan.Zero
            && position >= snapshot.CurrentTrack.Duration
        )
        {
            position = snapshot.CurrentTrack.Duration - TimeSpan.FromSeconds(1);
        }

        var result = playbackController.Seek(session, position);
        var direction = offset < TimeSpan.Zero ? "backward" : "forward";
        return Task.FromResult(
            DiscordResponses.PlaybackFeedback(
                result.IsSuccess
                    ? PlaybackCommandResult.Success(
                        $"Seeked {direction} by {offset.Duration().HumanizeSecond()}."
                    )
                    : result,
                session,
                result.IsSuccess ? position : null
            )
        );
    }

    private bool TryGetSession(out PlaybackSession session, out InteractionMessageProperties error)
    {
        session = null!;

        if (Context.Guild is not { } guild)
        {
            error = DiscordResponses.Ephemeral("The guild is not available. Try again later.");
            return false;
        }

        if (
            voiceConnections.Mapping.TryGetValue(guild.Id, out var voiceConnection)
            && voiceConnection is not null
        )
        {
            session = voiceConnection.PlaybackSession;
            error = null!;
            return true;
        }

        if (playbackService.TryGetPlaybackSession(guild.Id, out session))
        {
            error = null!;
            return true;
        }

        error = DiscordResponses.Ephemeral(
            "I do not have an active playback session for this guild. Use /play or /join from your voice channel."
        );
        return false;
    }
}
