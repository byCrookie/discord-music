using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Rewrite;

public class MusicSessionManager(
    IServiceProvider serviceProvider,
    ILogger<MusicSessionManager> logger)
{
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<ulong, MusicSession> _sessions = new();

    public async Task<ErrorOr<MusicSession>> JoinAsync(ApplicationCommandContext context,
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

    private async Task<ErrorOr<MusicSession>> GetOrCreateSessionAsync(GatewayClient client,
        Guild guild, TextChannel textChannel, VoiceGuildChannel voiceChannel, bool listen,
        CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        var sessionLogger = serviceProvider.GetRequiredService<ILogger<MusicSession>>();
        var queueLogger = serviceProvider.GetRequiredService<ILogger<Queue.Queue<AudioMetadata>>>();

        var queue = new Queue.Queue<AudioMetadata>(queueLogger);

        if (_sessions.TryGetValue(guild.Id, out var existingSession))
        {
            if (existingSession.VoiceSession.VoiceChannel.Id == voiceChannel.Id)
            {
                logger.LogDebug(
                    "Guild {Guild} already has a music session in voice channel {VoiceChannel}, reusing existing session",
                    guild.Name, voiceChannel.Name);

                if (existingSession.VoiceSession.VoiceClient.Status != WebSocketStatus.Ready)
                {
                    logger.LogDebug("Voice client for guild {Guild} is not ready, starting it",
                        guild.Name);
                    await existingSession.VoiceSession.VoiceClient.StartAsync(ct);
                }

                return existingSession;
            }

            logger.LogDebug(
                "Guild {Guild} already has a music session in voice channel {OldVoiceChannel}, moving to new voice channel {NewVoiceChannel}",
                guild.Name, existingSession.VoiceSession.VoiceChannel.Name, voiceChannel.Name);
            logger.LogDebug("Closing existing voice client for guild {Guild}", guild.Name);
            await existingSession.VoiceSession.DisposeAsync();

            logger.LogDebug("Joining voice channel {VoiceChannel} in guild {Guild}",
                guild.Name, voiceChannel.Name);
            var newVoiceClientForExisting = await client.JoinVoiceChannelAsync(guild.Id,
                voiceChannel.Id, new VoiceClientConfiguration
                {
                    ReceiveHandler = listen ? new VoiceReceiveHandler() : null,
                }, ct);
            logger.LogDebug(
                "Starting voice client for guild {Guild} and voice channel {VoiceChannel}",
                guild.Name, voiceChannel.Name);
            await newVoiceClientForExisting.StartAsync(ct);

            logger.LogDebug(
                "Updating voice client and voice channel for existing session in guild {Guild} to new voice channel {VoiceChannel}",
                guild.Name, voiceChannel.Name);

            var newOpusStreamForExisting = new OpusEncodeStream(
                newVoiceClientForExisting.CreateOutputStream(),
                PcmFormat.Short,
                VoiceChannels.Stereo,
                OpusApplication.Audio
            );

            existingSession.VoiceSession =
                new VoiceSession(newVoiceClientForExisting, voiceChannel, newOpusStreamForExisting);
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

        var session = new MusicSession(sessionLogger, queue, guild, textChannel,
            new VoiceSession(voiceClient, voiceChannel, opusStream));
        _sessions.Add(guild.Id, session);
        return session;
    }

    public async Task<ErrorOr<MusicSession>> GetSessionAsync(Guild guild, CancellationToken ct)
    {
        await using var _ = await _lock.AquireAsync(ct);

        if (_sessions.TryGetValue(guild.Id, out var session))
        {
            return session;
        }

        return Error.NotFound($"Music session for guild {guild.Name} not found.");
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
