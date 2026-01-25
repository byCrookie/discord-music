using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Sessions;

internal class GuildSessionManager(
    IServiceProvider serviceProvider,
    ILogger<GuildSessionManager> logger,
    RestClient restClient,
    VoiceCommandService voiceCommandService)
{
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<ulong, GuildSession> _sessions = new();

    public async Task<ErrorOr<GuildSession>> JoinAsync(ApplicationCommandContext context,
        bool listen,
        CancellationToken ct)
    {
        var userVoiceState = await TryGetGuildUserVoiceStateAsync(
            context.Client,
            context.Guild!.Id,
            context.User.Id,
            ct
        );

        if (userVoiceState.IsError)
        {
            return userVoiceState.Errors;
        }

        var voiceChannel = (VoiceGuildChannel)await context.Client.Rest.GetChannelAsync(
            userVoiceState.Value.ChannelId!.Value, cancellationToken: ct);

        var session = await GetOrCreateSessionAsync(context.Client,
            context.Guild!,
            context.Channel, voiceChannel, listen, ct);

        if (session.IsError)
        {
            return session.Errors;
        }

        return session.Value;
    }

    public async Task<ErrorOr<Success>> LeaveAsync(ulong guildId, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_sessions.TryGetValue(guildId, out var existingSession))
        {
            logger.LogDebug("Leaving voice channel {VoiceChannel} in guild {Guild}",
                existingSession.GuildVoiceSession.VoiceChannel.Name,
                existingSession.Guild.Name);
            await existingSession.GuildVoiceSession.DisposeAsync();
            return Result.Success;
        }

        logger.LogDebug("No existing session for guild {Guild}", guildId);
        return Error.NotFound($"No existing session for guild {guildId}");
    }

    private async Task<ErrorOr<GuildSession>> GetOrCreateSessionAsync(GatewayClient client,
        Guild guild, TextChannel textChannel, VoiceGuildChannel voiceChannel, bool listen,
        CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);
        
        if (_sessions.TryGetValue(guild.Id, out var existingSession))
        {
            if (listen)
                voiceCommandService.Subscribe(existingSession);

            if (existingSession.GuildVoiceSession.VoiceChannel.Id == voiceChannel.Id)
            {
                logger.LogDebug(
                    "Guild {Guild} already has a music session in voice channel {VoiceChannel}, reusing existing session",
                    guild.Name, voiceChannel.Name);

                if (existingSession.GuildVoiceSession.VoiceClient.Status != WebSocketStatus.Ready)
                {
                    logger.LogDebug("Voice client for guild {Guild} is not ready, starting it",
                        guild.Name);
                    await existingSession.GuildVoiceSession.VoiceClient.StartAsync(ct);
                }

                return existingSession;
            }

            logger.LogDebug(
                "Guild {Guild} already has a music session in voice channel {OldVoiceChannel}, moving to new voice channel {NewVoiceChannel}",
                guild.Name, existingSession.GuildVoiceSession.VoiceChannel.Name, voiceChannel.Name);
            logger.LogDebug("Closing existing voice client for guild {Guild}", guild.Name);
            await existingSession.GuildVoiceSession.DisposeAsync();

            logger.LogDebug("Joining voice channel {VoiceChannel} in guild {Guild}",
                guild.Name, voiceChannel.Name);
            var voiceClientForExisting = await client.JoinVoiceChannelAsync(guild.Id,
                voiceChannel.Id, new VoiceClientConfiguration
                {
                    ReceiveHandler = listen ? new VoiceReceiveHandler() : null,
                }, ct);
            logger.LogDebug(
                "Starting voice client for guild {Guild} and voice channel {VoiceChannel}",
                guild.Name, voiceChannel.Name);
            await voiceClientForExisting.StartAsync(ct);

            logger.LogDebug(
                "Updating voice client and voice channel for existing session in guild {Guild} to new voice channel {VoiceChannel}",
                guild.Name, voiceChannel.Name);

            var opusStreamForExisting = new OpusEncodeStream(
                voiceClientForExisting.CreateOutputStream(),
                PcmFormat.Short,
                VoiceChannels.Stereo,
                OpusApplication.Audio
            );

            var audioPlayerForExisting =
                ActivatorUtilities.CreateInstance<AudioPlayer>(serviceProvider,
                    opusStreamForExisting);

            await existingSession.UpdateGuildVoiceSessionAsync(new GuildVoiceSession(
                voiceClientForExisting, voiceChannel, opusStreamForExisting,
                audioPlayerForExisting), ct);
            
            if (listen)
                voiceCommandService.Subscribe(existingSession);
            
            return existingSession;
        }

        logger.LogDebug("Joining voice channel {VoiceChannel} in guild {Guild}",
            guild.Name, voiceChannel.Name);

        var voiceClient = await client.JoinVoiceChannelAsync(guild.Id, voiceChannel.Id,
            new VoiceClientConfiguration
            {
                ReceiveHandler = listen ? new VoiceReceiveHandler() : null,
            }, ct);

        logger.LogDebug("Starting voice client for guild {Guild} and voice channel {VoiceChannel}",
            guild.Name, voiceChannel.Name);
        await voiceClient.StartAsync(ct);

        var opusStream = new OpusEncodeStream(
            voiceClient.CreateOutputStream(),
            PcmFormat.Short,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );

        var audioPlayer =
            ActivatorUtilities.CreateInstance<AudioPlayer>(serviceProvider, opusStream);
        var session = ActivatorUtilities.CreateInstance<GuildSession>(serviceProvider, guild,
            textChannel,
            new GuildVoiceSession(voiceClient, voiceChannel, opusStream, audioPlayer));
        
        if (listen)
            voiceCommandService.Subscribe(session);
        
        _sessions.Add(guild.Id, session);
        return session;
    }

    public async Task<ErrorOr<GuildSession>> GetSessionAsync(ulong guildId, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_sessions.TryGetValue(guildId, out var session))
        {
            return session;
        }

        var guild = await restClient.GetGuildAsync(guildId, cancellationToken: ct);
        return Error.NotFound($"No session found for guild {guild.Name} ({guild.Id}). Join first.");
    }

    private static async Task<ErrorOr<VoiceState>> TryGetGuildUserVoiceStateAsync(
        GatewayClient gatewayClient, ulong guildId, ulong userId, CancellationToken ct)
    {
        try
        {
            return await gatewayClient.Rest.GetGuildUserVoiceStateAsync(guildId, userId,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            return Error.NotFound(
                description:
                $"Could not get voice state for user {userId} in guild {guildId}: {ex.Message}");
        }
    }
}
