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
                return;
            }
            catch (RestException ex)
            {
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
    }

    public async Task SendPublicAsync(
        DiscordRequestOrigin origin,
        string message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await restClient.SendMessageAsync(
                origin.ChannelId,
                DiscordResponses.ChannelMessage(message),
                cancellationToken: cancellationToken
            );
        }
        catch (RestException ex)
        {
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
}
