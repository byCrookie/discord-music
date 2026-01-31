using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.VoiceCommands;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Sessions;

internal class GuildSessionManager(
    IServiceProvider serviceProvider,
    ILogger<GuildSessionManager> logger,
    GatewayClient gatewayClient,
    IVoiceCommandSubscriptions voiceCommandSubscriptions
)
{
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<ulong, GuildSession> _sessions = new();

    public async Task<ErrorOr<GuildSession>> JoinAsync(
        ApplicationCommandContext context,
        VoiceCommandSetting? voiceCommandSetting,
        CancellationToken ct
    )
    {
        await using var _ = await _lock.AquireAsync(ct);

        var userVoiceState = await TryGetGuildUserVoiceStateAsync(
            logger,
            context.Client,
            context.Guild!.Id,
            context.User.Id,
            ct
        );

        if (userVoiceState.IsError)
        {
            return userVoiceState.Errors;
        }

        var voiceChannel = (VoiceGuildChannel)
            await context.Client.Rest.GetChannelAsync(
                userVoiceState.Value.ChannelId!.Value,
                cancellationToken: ct
            );

        var session = await GetOrCreateSessionAsync(
            context.Client,
            context.Guild!,
            context.Channel,
            voiceChannel,
            voiceCommandSetting,
            ct
        );

        if (session.IsError)
        {
            return session.Errors;
        }

        return session.Value;
    }

    public async Task<ErrorOr<Success>> LeaveAsync(ulong guildId, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);
        return await LeaveAndDisposeVoiceClientAsync(guildId, ct);
    }

    public async Task<ErrorOr<GuildSession>> GetSessionAsync(ulong guildId, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_sessions.TryGetValue(guildId, out var session))
        {
            return session;
        }

        var guild = await gatewayClient.Rest.GetGuildAsync(guildId, cancellationToken: ct);
        return Error.NotFound(
            description: $"No active music session for **{guild.Name}**. Use `/join` first."
        );
    }

    private async Task<ErrorOr<Success>> LeaveAndDisposeVoiceClientAsync(
        ulong guildId,
        CancellationToken ct
    )
    {
        if (_sessions.TryGetValue(guildId, out var existingSession))
        {
            logger.LogDebug(
                "Leaving voice channel {VoiceChannel} in guild {Guild}",
                existingSession.GuildVoiceSession.VoiceChannel.Name,
                existingSession.Guild.Name
            );

            voiceCommandSubscriptions.Remove(guildId);
            await existingSession.GuildVoiceSession.DisposeAsync();

            await gatewayClient.CloseAsync(cancellationToken: ct);
            await gatewayClient.StartAsync(cancellationToken: ct);

            return Result.Success;
        }

        await gatewayClient.CloseAsync(cancellationToken: ct);
        await gatewayClient.StartAsync(cancellationToken: ct);

        logger.LogDebug("No existing session for guild {Guild}", guildId);
        return Error.NotFound(description: "I’m not connected in this server.");
    }

    private async Task<ErrorOr<GuildSession>> GetOrCreateSessionAsync(
        GatewayClient client,
        Guild guild,
        TextChannel textChannel,
        VoiceGuildChannel voiceChannel,
        VoiceCommandSetting? voiceCommandSetting,
        CancellationToken ct
    )
    {
        if (_sessions.TryGetValue(guild.Id, out var existingSession))
        {
            if (existingSession.GuildVoiceSession.VoiceChannel.Id == voiceChannel.Id)
            {
                logger.LogDebug(
                    "Guild {Guild} already has a music session in voice channel {VoiceChannel}, reusing existing session",
                    guild.Name,
                    voiceChannel.Name
                );

                if (existingSession.GuildVoiceSession.VoiceClient.Status != WebSocketStatus.Ready)
                {
                    logger.LogDebug(
                        "Voice client for guild {Guild} is not ready, starting it",
                        guild.Name
                    );
                    await existingSession.GuildVoiceSession.VoiceClient.StartAsync(ct);
                    await existingSession.GuildVoiceSession.VoiceClient.EnterSpeakingStateAsync(
                        new SpeakingProperties(SpeakingFlags.Priority),
                        cancellationToken: ct
                    );
                }

                UpdateListenBasedOnSession(
                    voiceCommandSetting,
                    guild,
                    existingSession.GuildVoiceSession.VoiceClient
                );

                return existingSession;
            }

            logger.LogDebug(
                "Guild {Guild} already has a music session in voice channel {OldVoiceChannel}, moving to new voice channel {NewVoiceChannel}",
                guild.Name,
                existingSession.GuildVoiceSession.VoiceChannel.Name,
                voiceChannel.Name
            );
            logger.LogDebug("Closing existing voice client for guild {Guild}", guild.Name);

            voiceCommandSubscriptions.Remove(guild.Id);
            await existingSession.GuildVoiceSession.DisposeAsync();

            logger.LogDebug(
                "Joining voice channel {VoiceChannel} in guild {Guild}",
                guild.Name,
                voiceChannel.Name
            );
            var voiceClientForExisting = await client.JoinVoiceChannelAsync(
                guild.Id,
                voiceChannel.Id,
                new VoiceClientConfiguration
                {
                    ReceiveHandler = ShouldListen(voiceCommandSetting, guild)
                        ? new VoiceReceiveHandler()
                        : null,
                },
                ct
            );
            logger.LogDebug(
                "Starting voice client for guild {Guild} and voice channel {VoiceChannel}",
                guild.Name,
                voiceChannel.Name
            );
            await voiceClientForExisting.StartAsync(ct);
            await voiceClientForExisting.EnterSpeakingStateAsync(
                new SpeakingProperties(SpeakingFlags.Priority),
                cancellationToken: ct
            );

            UpdateListenBasedOnSession(voiceCommandSetting, guild, voiceClientForExisting);

            logger.LogDebug(
                "Updating voice client and voice channel for existing session in guild {Guild} to new voice channel {VoiceChannel}",
                guild.Name,
                voiceChannel.Name
            );

            var opusStreamForExisting = new OpusEncodeStream(
                voiceClientForExisting.CreateOutputStream(),
                PcmFormat.Short,
                VoiceChannels.Stereo,
                OpusApplication.Audio
            );

            var audioPlayerForExisting = ActivatorUtilities.CreateInstance<AudioPlayer>(
                serviceProvider,
                opusStreamForExisting
            );

            await existingSession.UpdateGuildVoiceSessionAsync(
                new GuildVoiceSession(
                    voiceClientForExisting,
                    voiceChannel,
                    opusStreamForExisting,
                    audioPlayerForExisting
                ),
                ct
            );

            return existingSession;
        }

        logger.LogDebug(
            "Joining voice channel {VoiceChannel} in guild {Guild}",
            guild.Name,
            voiceChannel.Name
        );

        var voiceClient = await client.JoinVoiceChannelAsync(
            guild.Id,
            voiceChannel.Id,
            new VoiceClientConfiguration
            {
                ReceiveHandler = ShouldListen(voiceCommandSetting, guild)
                    ? new VoiceReceiveHandler()
                    : null,
            },
            ct
        );

        logger.LogDebug(
            "Starting voice client for guild {Guild} and voice channel {VoiceChannel}",
            guild.Name,
            voiceChannel.Name
        );
        await voiceClient.StartAsync(ct);
        await voiceClient.EnterSpeakingStateAsync(
            new SpeakingProperties(SpeakingFlags.Priority),
            cancellationToken: ct
        );

        UpdateListenBasedOnSession(voiceCommandSetting, guild, voiceClient);

        var opusStream = new OpusEncodeStream(
            voiceClient.CreateOutputStream(),
            PcmFormat.Short,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );

        var audioPlayer = ActivatorUtilities.CreateInstance<AudioPlayer>(
            serviceProvider,
            opusStream
        );
        var session = ActivatorUtilities.CreateInstance<GuildSession>(
            serviceProvider,
            guild,
            textChannel,
            new GuildVoiceSession(voiceClient, voiceChannel, opusStream, audioPlayer)
        );

        _sessions.Add(guild.Id, session);
        return session;
    }

    private static async Task<ErrorOr<VoiceState>> TryGetGuildUserVoiceStateAsync(
        ILogger logger,
        GatewayClient gatewayClient,
        ulong guildId,
        ulong userId,
        CancellationToken ct
    )
    {
        try
        {
            return await gatewayClient.Rest.GetGuildUserVoiceStateAsync(
                guildId,
                userId,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            // Keep Discord-facing message friendly, but log the underlying exception.
            logger.LogWarning(
                ex,
                "Failed to get voice state for user {UserId} in guild {GuildId}",
                userId,
                guildId
            );
            return Error.NotFound(
                description: "I couldn’t figure out which voice channel you’re in. Please join a voice channel and try again."
            );
        }
    }

    private bool ShouldListen(VoiceCommandSetting? voiceCommandSetting, Guild guild)
    {
        switch (voiceCommandSetting)
        {
            case null:
                if (voiceCommandSubscriptions.Has(guild.Id))
                {
                    return true;
                }

                voiceCommandSubscriptions.Remove(guild.Id);
                break;
            case VoiceCommandSetting.Yes:
                return true;
            case VoiceCommandSetting.No:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(voiceCommandSetting),
                    voiceCommandSetting,
                    null
                );
        }

        return false;
    }

    private void UpdateListenBasedOnSession(
        VoiceCommandSetting? voiceCommandSetting,
        Guild guild,
        VoiceClient voiceClient
    )
    {
        if (ShouldListen(voiceCommandSetting, guild))
        {
            logger.LogDebug(
                "Ensuring voice command subscription for guild {Guild} is active",
                guild.Name
            );
            voiceCommandSubscriptions.Set(guild.Id, voiceClient);
            return;
        }

        logger.LogDebug("Removing voice command subscription for guild {Guild}", guild.Name);
        voiceCommandSubscriptions.Remove(guild.Id);
    }
}
