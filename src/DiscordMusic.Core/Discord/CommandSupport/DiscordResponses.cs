using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils;
using NetCord;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal static class DiscordResponses
{
    public static InteractionMessageProperties Ephemeral(string content)
    {
        return new InteractionMessageProperties()
            .WithContent(content)
            .WithFlags(MessageFlags.Ephemeral);
    }

    public static InteractionMessageProperties Public(string content)
    {
        return new InteractionMessageProperties().WithContent(content);
    }

    public static MessageProperties ChannelMessage(string content)
    {
        return new MessageProperties().WithContent(content);
    }

    public static InteractionMessageProperties FromPlaybackResult(PlaybackCommandResult result)
    {
        return Ephemeral(result.Message);
    }

    public static InteractionMessageProperties PlaybackFeedback(
        PlaybackCommandResult result,
        PlaybackSession session,
        TimeSpan? positionOverride = null
    )
    {
        var snapshot = session.Snapshot();
        if (!result.IsSuccess || snapshot.CurrentTrack is null)
        {
            return Ephemeral(result.Message);
        }

        return Ephemeral(
            $"""
            {result.Message}

            {AudioBarRenderer.Render(snapshot, positionOverride)}
            """
        );
    }

    public static string FormatTrack(Track track, bool includeDuration = true)
    {
        var duration =
            includeDuration && track.Duration > TimeSpan.Zero
                ? $" [{track.Duration.HumanizeSecond()}]"
                : string.Empty;
        return $"{track.Name} - {track.Artists}{duration}";
    }
}
