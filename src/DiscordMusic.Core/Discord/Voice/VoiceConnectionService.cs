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
        if (
            voiceConnections.Mapping.TryGetValue(guildId, out var existingConnection)
            && existingConnection is not null
        )
        {
            return VoiceConnectionResult.AlreadyConnected(existingConnection);
        }

        if (voiceConnections.Mapping.ContainsKey(guildId))
        {
            return VoiceConnectionResult.Failed("A voice connection is already starting.");
        }

        var channelId = requestedChannelId ?? GetUserChannelId(voiceStates, userId);
        if (channelId is null)
        {
            return VoiceConnectionResult.Failed(
                "You must specify a channel or be connected to a voice channel."
            );
        }

        if (!voiceConnections.Mapping.TryAdd(guildId, null))
        {
            return VoiceConnectionResult.Failed("A voice connection is already starting.");
        }

        VoiceClient voiceClient;
        try
        {
            voiceClient = await client.JoinVoiceChannelAsync(
                guildId,
                channelId.Value,
                new VoiceClientConfiguration { Logger = new ConsoleLogger() }
            );
        }
        catch
        {
            voiceConnections.Mapping.TryRemove(
                item: new KeyValuePair<ulong, VoiceConnection?>(guildId, null)
            );

            await client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
            throw;
        }

        var voiceConnection = new VoiceConnection(voiceClient);
        if (!voiceConnections.Mapping.TryUpdate(guildId, voiceConnection, null))
        {
            voiceConnection.Dispose();
            await client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
            return VoiceConnectionResult.Failed("Failed to register voice connection.");
        }

        try
        {
            await voiceClient.StartAsync();
        }
        catch
        {
            if (
                !voiceConnections.Mapping.TryRemove(
                    item: new KeyValuePair<ulong, VoiceConnection?>(guildId, voiceConnection)
                )
            )
            {
                throw;
            }

            voiceConnection.Dispose();
            await client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
            throw;
        }

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
        return VoiceConnectionResult.Connected(voiceConnection);
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
