using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord;

internal class VoiceStateUpdateHandler(
    ILogger<VoiceStateUpdateHandler> logger,
    Cancellation cancellation,
    RestClient restClient,
    GuildSessionManager guildSessionManager
) : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState voiceState)
    {
        var ct = cancellation.CancellationToken;
        var bot = await restClient.GetCurrentUserAsync(cancellationToken: ct);

        if (voiceState.UserId == bot.Id && voiceState.ChannelId is not null)
        {
            logger.LogInformation("Bot joined voice channel {ChannelId}", voiceState.ChannelId);
            return;
        }

        if (voiceState.UserId == bot.Id && voiceState.ChannelId is not null)
        {
            logger.LogInformation("Bot left voice channel");
            return;
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

        if (!session.Value.Guild.VoiceStates.TryGetValue(bot.Id, out var voiceStateBot))
        {
            logger.LogInformation("Bot is not in a voice channel.");
            return;
        }

        var voiceStatesInChannel = session.Value.Guild
            .VoiceStates.Where(vs =>
                vs.Value.ChannelId == voiceStateBot.ChannelId && vs.Value.UserId != bot.Id
            )
            .ToList();

        if (voiceStatesInChannel.Count != 0)
        {
            logger.LogInformation(
                "Channel {ChannelId} is still active. {Count} members are still in the channel. Active: {Members}",
                voiceStateBot.ChannelId,
                voiceStatesInChannel.Count,
                string.Join(", ", voiceStatesInChannel.Select(vs => vs.Value.UserId))
            );
            return;
        }

        logger.LogInformation("Bot is alone in the voice channel. Disconnecting.");
        await guildSessionManager.LeaveAsync(voiceState.GuildId, ct);
    }
}
