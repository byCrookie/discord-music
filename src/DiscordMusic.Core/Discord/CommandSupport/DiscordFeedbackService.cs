using System.Diagnostics;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Tracks;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal sealed class DiscordFeedbackService(
    RestClient restClient,
    ILogger<DiscordFeedbackService> logger
) : IDiscordFeedbackService
{
    public async Task SendPrivateAsync(
        DiscordRequestOrigin origin,
        string message,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartActivity(origin, "private");
        var result = "sent";

        if (origin is { ApplicationId: not 0, InteractionToken.Length: > 0 })
        {
            try
            {
                await restClient.SendInteractionFollowupMessageAsync(
                    origin.ApplicationId,
                    origin.InteractionToken,
                    DiscordResponses.Ephemeral(message),
                    cancellationToken: cancellationToken
                );
                Record(origin, "interaction_followup", "sent");
                activity?.SetStatus(ActivityStatusCode.Ok);
                DiscordMusicObservability.SetTag(activity, "result", result);
                DiscordMusicObservability.SetTag(
                    activity,
                    "discord.feedback.target",
                    "interaction_followup"
                );
                return;
            }
            catch (RestException ex)
            {
                result = "fallback";
                activity?.AddException(ex);
                DiscordMusicObservability.SetTag(
                    activity,
                    "discord.feedback.interaction_followup.result",
                    "failed"
                );
                Record(origin, "interaction_followup", "failed");
                logger.LogWarning(
                    ex,
                    "Could not send private interaction follow-up. Falling back to channel message. GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId}",
                    origin.GuildId,
                    origin.ChannelId,
                    origin.UserId
                );
            }
        }

        await SendPublicAsync(origin, message, cancellationToken);
        activity?.SetStatus(ActivityStatusCode.Ok, result);
        DiscordMusicObservability.SetTag(activity, "result", result);
    }

    public async Task SendPublicAsync(
        DiscordRequestOrigin origin,
        string message,
        CancellationToken cancellationToken
    )
    {
        using var activity = StartActivity(origin, "channel_message");
        try
        {
            await restClient.SendMessageAsync(
                origin.ChannelId,
                DiscordResponses.ChannelMessage(message),
                cancellationToken: cancellationToken
            );
            Record(origin, "channel_message", "sent");
            activity?.SetStatus(ActivityStatusCode.Ok);
            DiscordMusicObservability.SetTag(activity, "result", "sent");
        }
        catch (RestException ex)
        {
            Record(origin, "channel_message", "failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            DiscordMusicObservability.SetTag(activity, "result", "failed");
            logger.LogWarning(
                ex,
                "Could not send feedback message to Discord channel. GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId}",
                origin.GuildId,
                origin.ChannelId,
                origin.UserId
            );
        }
    }

    public Task SendPlaybackFailureAsync(
        DiscordRequestOrigin origin,
        Track track,
        CancellationToken cancellationToken
    )
    {
        return SendPrivateAsync(
            origin,
            $"Playback failed for **{DiscordResponses.FormatTrack(track)}**. I skipped it and will continue with the next queued track.",
            cancellationToken
        );
    }

    private static Activity? StartActivity(DiscordRequestOrigin origin, string target)
    {
        var activity = DiscordMusicObservability.StartActivity(
            "discord.feedback.send",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetGuildTag(activity, origin.GuildId);
        DiscordMusicObservability.SetTag(
            activity,
            "discord.channel.id",
            origin.ChannelId.ToString()
        );
        DiscordMusicObservability.SetTag(activity, "discord.user.id", origin.UserId.ToString());
        DiscordMusicObservability.SetTag(activity, "discord.feedback.target", target);
        return activity;
    }

    private static void Record(DiscordRequestOrigin origin, string target, string result)
    {
        var tags = DiscordMusicObservability.GuildTags(origin.GuildId);
        tags.Add("discord.channel.id", origin.ChannelId.ToString());
        tags.Add("discord.feedback.target", target);
        tags.Add("result", result);
        DiscordMusicObservability.DiscordFeedbackMessages.Add(1, tags);
    }
}
