using System.Diagnostics;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;

namespace DiscordMusic.Core.Discord.Voice;

internal sealed class VoiceConnectionService(
    ILogger<VoiceConnectionService> logger,
    VoiceConnectionRegistry voiceConnections,
    PlaybackService playbackService
)
{
    public async Task<VoiceConnectionResult> JoinUserChannelAsync(
        GatewayClient client,
        ulong guildId,
        IReadOnlyDictionary<ulong, VoiceState> voiceStates,
        ulong userId,
        ulong? requestedChannelId = null
    )
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "failed";
        using var activity = DiscordMusicObservability.StartActivity(
            "discord.voice.join",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetGuildTag(activity, guildId);
        DiscordMusicObservability.SetTag(activity, "discord.user.id", userId.ToString());

        if (
            voiceConnections.Mapping.TryGetValue(guildId, out var existingConnection)
            && existingConnection is not null
        )
        {
            result = "already_connected";
            activity?.SetStatus(ActivityStatusCode.Ok, result);
            return VoiceConnectionResult.AlreadyConnected(existingConnection);
        }

        if (voiceConnections.Mapping.ContainsKey(guildId))
        {
            result = "already_starting";
            activity?.SetStatus(ActivityStatusCode.Ok, result);
            return VoiceConnectionResult.Failed("A voice connection is already starting.");
        }

        var channelId = requestedChannelId ?? GetUserChannelId(voiceStates, userId);
        DiscordMusicObservability.SetTag(activity, "discord.channel.id", channelId?.ToString());
        if (channelId is null)
        {
            result = "missing_channel";
            activity?.SetStatus(ActivityStatusCode.Ok, result);
            return VoiceConnectionResult.Failed(
                "You must specify a channel or be connected to a voice channel."
            );
        }

        if (!voiceConnections.Mapping.TryAdd(guildId, null))
        {
            result = "already_starting";
            activity?.SetStatus(ActivityStatusCode.Ok, result);
            return VoiceConnectionResult.Failed("A voice connection is already starting.");
        }

        VoiceConnection? startedConnection = null;
        try
        {
            var voiceClient = await client.JoinVoiceChannelAsync(
                guildId,
                channelId.Value,
                new VoiceClientConfiguration { Logger = new ConsoleLogger() }
            );

            var voiceConnection = new VoiceConnection(voiceClient);
            startedConnection = voiceConnection;
            if (!voiceConnections.Mapping.TryUpdate(guildId, voiceConnection, null))
            {
                voiceConnection.Dispose();
                await client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
                result = "registration_failed";
                activity?.SetStatus(ActivityStatusCode.Error, result);
                return VoiceConnectionResult.Failed("Failed to register voice connection.");
            }

            await voiceClient.StartAsync();

            voiceClient.Disconnect += args =>
            {
                if (args.Reconnect)
                {
                    return default;
                }

                if (
                    voiceConnections.Mapping.TryRemove(
                        item: new KeyValuePair<ulong, VoiceConnection?>(guildId, voiceConnection)
                    )
                )
                {
                    logger.LogInformation("Voice client disconnected. GuildId={GuildId}", guildId);
                    playbackService.Stop(guildId);
                    voiceConnection.Dispose();
                }

                return default;
            };

            playbackService.Start(guildId, voiceConnection);
            result = "connected";
            activity?.SetStatus(ActivityStatusCode.Ok);
            return VoiceConnectionResult.Connected(voiceConnection);
        }
        catch (Exception ex)
        {
            var removed = startedConnection is null
                ? voiceConnections.Mapping.TryRemove(
                    item: new KeyValuePair<ulong, VoiceConnection?>(guildId, null)
                )
                : voiceConnections.Mapping.TryRemove(
                    item: new KeyValuePair<ulong, VoiceConnection?>(guildId, startedConnection)
                );

            if (removed)
            {
                startedConnection?.Dispose();
                await client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
            }

            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            var tags = DiscordMusicObservability.GuildTags(guildId);
            tags.Add("result", result);
            DiscordMusicObservability.VoiceConnections.Add(1, tags);
            DiscordMusicObservability.VoiceConnectionDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                tags
            );
        }
    }

    private static ulong? GetUserChannelId(
        IReadOnlyDictionary<ulong, VoiceState> voiceStates,
        ulong userId
    )
    {
        return voiceStates.TryGetValue(userId, out var voiceState) ? voiceState.ChannelId : null;
    }
}

internal sealed record VoiceConnectionResult(
    VoiceConnection? Connection,
    VoiceConnectionResultStatus Status,
    string Message
)
{
    public bool Succeeded =>
        Status
            is VoiceConnectionResultStatus.Connected
                or VoiceConnectionResultStatus.AlreadyConnected;

    public static VoiceConnectionResult Connected(VoiceConnection connection)
    {
        return new(connection, VoiceConnectionResultStatus.Connected, "Joined voice channel.");
    }

    public static VoiceConnectionResult AlreadyConnected(VoiceConnection connection)
    {
        return new(
            connection,
            VoiceConnectionResultStatus.AlreadyConnected,
            "Already connected to a voice channel in this guild."
        );
    }

    public static VoiceConnectionResult Failed(string message)
    {
        return new(null, VoiceConnectionResultStatus.Failed, message);
    }
}

internal enum VoiceConnectionResultStatus
{
    Connected,
    AlreadyConnected,
    Failed,
}
