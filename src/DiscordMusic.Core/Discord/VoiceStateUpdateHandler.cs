using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace DiscordMusic.Core.Discord;

internal class VoiceStateUpdateHandler(
    ILogger<VoiceStateUpdateHandler> logger,
    Cancellation cancellation,
    GatewayClient gatewayClient,
    GuildSessionManager guildSessionManager
) : IVoiceStateUpdateGatewayHandler
{
    private ulong? _botId;

    public async ValueTask HandleAsync(VoiceState voiceState)
    {
        var ct = cancellation.CancellationToken;

        // Cache the bot id (REST call) so we don't fetch it on every gateway event.
        var botId = _botId ??= (
            await gatewayClient.Rest.GetCurrentUserAsync(cancellationToken: ct)
        ).Id;

        var previousChannelId =
            gatewayClient.Cache.Guilds.TryGetValue(voiceState.GuildId, out var g)
            && g.VoiceStates.TryGetValue(voiceState.UserId, out var vs)
                ? vs.ChannelId
                : null;

        var change = VoiceStateChangeClassifier.Classify(
            voiceState.UserId,
            botId,
            previousChannelId,
            voiceState.ChannelId
        );

        if (voiceState.UserId == botId)
        {
            switch (change)
            {
                case VoiceStateChange.Joined:
                    logger.LogInformation(
                        "Bot joined voice channel {ChannelId}",
                        voiceState.ChannelId
                    );
                    return;
                case VoiceStateChange.Left:
                    logger.LogInformation("Bot left voice channel");
                    return;
                case VoiceStateChange.Moved:
                    logger.LogInformation(
                        "Bot moved voice channels {FromChannelId} -> {ToChannelId}",
                        previousChannelId,
                        voiceState.ChannelId
                    );
                    return;
            }
        }

        if (voiceState.ChannelId is not null)
        {
            logger.LogInformation(
                "User {UserId} joined voice channel {ChannelId}",
                voiceState.UserId,
                voiceState.ChannelId
            );
        }
        else
        {
            logger.LogInformation("User {UserId} left voice channel", voiceState.UserId);
        }

        var session = await guildSessionManager.GetSessionAsync(voiceState.GuildId, ct);

        if (session.IsError)
        {
            logger.LogInformation("No active session for guild {GuildId}", voiceState.GuildId);
            return;
        }

        if (!gatewayClient.Cache.Guilds.TryGetValue(voiceState.GuildId, out var guild))
        {
            logger.LogInformation("Guild {GuildId} not found in cache", voiceState.GuildId);
            return;
        }

        if (!guild.VoiceStates.TryGetValue(botId, out var voiceStateBot))
        {
            logger.LogInformation("Bot is not in a voice channel.");
            return;
        }

        var voiceStatesInChannel = guild
            .VoiceStates.Where(vsLocal =>
                vsLocal.Value.ChannelId == voiceStateBot.ChannelId && vsLocal.Value.UserId != botId
            )
            .ToList();

        if (voiceStatesInChannel.Count != 0)
        {
            logger.LogInformation(
                "Channel {ChannelId} is still active. {Count} members are still in the channel. Active: {Members}",
                voiceStateBot.ChannelId,
                voiceStatesInChannel.Count,
                string.Join(", ", voiceStatesInChannel.Select(vsLocal => vsLocal.Value.UserId))
            );
            return;
        }

        logger.LogInformation("Bot is alone in the voice channel. Disconnecting.");
        await guildSessionManager.LeaveAsync(voiceState.GuildId, ct);
    }
}
