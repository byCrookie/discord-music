using System.Diagnostics;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace DiscordMusic.Core.Discord;

internal class VoiceStateUpdateHandler(
    ILogger<VoiceStateUpdateHandler> logger,
    Cancellation cancellation,
    GatewayClient gatewayClient,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService
) : IVoiceStateUpdateGatewayHandler
{
    public async ValueTask HandleAsync(VoiceState voiceState)
    {
        using var activity = DiscordMusicObservability.StartActivity("discord.voice_state.update");
        DiscordMusicObservability.SetGuildTag(activity, voiceState.GuildId);
        DiscordMusicObservability.SetTag(activity, "discord.user.id", voiceState.UserId.ToString());
        DiscordMusicObservability.SetTag(
            activity,
            "discord.channel.id",
            voiceState.ChannelId?.ToString()
        );

        try
        {
            var ct = cancellation.CancellationToken;
            var bot = await gatewayClient.Rest.GetCurrentUserAsync(cancellationToken: ct);
            DiscordMusicObservability.SetTag(activity, "discord.bot.user.id", bot.Id.ToString());

            if (voiceState.UserId == bot.Id && voiceState.ChannelId is not null)
            {
                logger.LogInformation("Bot joined voice channel {ChannelId}", voiceState.ChannelId);
                activity?.SetStatus(ActivityStatusCode.Ok, "bot_joined");
                return;
            }

            if (voiceState.UserId == bot.Id && voiceState.ChannelId is null)
            {
                logger.LogInformation("Bot left voice channel");
                activity?.SetStatus(ActivityStatusCode.Ok, "bot_left");
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

            if (!gatewayClient.Cache.Guilds.TryGetValue(voiceState.GuildId, out var guild))
            {
                logger.LogInformation("Guild {GuildId} not found in cache", voiceState.GuildId);
                activity?.SetStatus(ActivityStatusCode.Ok, "missing_guild_cache");
                return;
            }

            if (!guild.VoiceStates.TryGetValue(bot.Id, out var voiceStateBot))
            {
                logger.LogInformation("Bot is not in a voice channel.");
                activity?.SetStatus(ActivityStatusCode.Ok, "bot_not_in_voice");
                return;
            }

            var voiceStatesInChannel = guild
                .VoiceStates.Where(vs =>
                    vs.Value.ChannelId == voiceStateBot.ChannelId && vs.Value.UserId != bot.Id
                )
                .ToList();
            DiscordMusicObservability.SetTag(
                activity,
                "discord.voice.channel.member_count",
                voiceStatesInChannel.Count
            );

            if (voiceStatesInChannel.Count != 0)
            {
                logger.LogInformation(
                    "Channel {ChannelId} is still active. {Count} members are still in the channel. Active: {Members}",
                    voiceStateBot.ChannelId,
                    voiceStatesInChannel.Count,
                    string.Join(", ", voiceStatesInChannel.Select(vs => vs.Value.UserId))
                );
                activity?.SetStatus(ActivityStatusCode.Ok, "channel_active");
                return;
            }

            logger.LogInformation("Bot is alone in the voice channel. Disconnecting.");

            if (
                !voiceInstances.Mapping.TryGetValue(voiceState.GuildId, out var voiceInstance)
                || voiceInstance is null
            )
            {
                logger.LogInformation(
                    "No voice instance found for guild {GuildId}. Nothing to disconnect.",
                    voiceState.GuildId
                );
                DiscordMusicObservability.RecordVoiceDisconnect(
                    voiceState.GuildId,
                    "alone_missing_connection"
                );
                activity?.SetStatus(ActivityStatusCode.Ok, "alone_missing_connection");
                return;
            }

            if (
                voiceInstances.Mapping.TryRemove(
                    item: new KeyValuePair<ulong, VoiceConnection?>(
                        voiceState.GuildId,
                        voiceInstance
                    )
                )
            )
            {
                DiscordMusicObservability.RecordVoiceDisconnect(voiceState.GuildId, "alone");
                using var disconnectActivity = StartDisconnectActivity(voiceState.GuildId, "alone");
                try
                {
                    playbackService.Stop(voiceState.GuildId);
                    await voiceInstance.Client.CloseAsync(cancellationToken: ct);
                    disconnectActivity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    disconnectActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    disconnectActivity?.AddException(ex);
                    throw;
                }
                finally
                {
                    voiceInstance.Dispose();
                    await gatewayClient.UpdateVoiceStateAsync(
                        new VoiceStateProperties(voiceState.GuildId, null),
                        cancellationToken: ct
                    );
                }

                activity?.SetStatus(ActivityStatusCode.Ok, "alone_disconnected");
                return;
            }

            activity?.SetStatus(ActivityStatusCode.Ok, "connection_already_removed");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static Activity? StartDisconnectActivity(ulong guildId, string reason)
    {
        var activity = DiscordMusicObservability.StartActivity("discord.voice.disconnect");
        DiscordMusicObservability.SetGuildTag(activity, guildId);
        DiscordMusicObservability.SetTag(activity, "reason", reason);
        return activity;
    }
}
