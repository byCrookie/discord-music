using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandService(
    ILogger<VoiceCommandService> logger,
    WhisperVoiceTranscriber transcriber,
    SimpleVoiceCommandParser parser,
    VoiceCommandDispatcher dispatcher,
    VoiceCommandManager manager
) : BackgroundService
{
    private readonly AsyncLock _transcribeGate = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(250);

        const int bytesPerSecond = 16000 * 2;
        var flushAfterSilence = TimeSpan.FromSeconds(1);
        const int minTranscribeBytes = bytesPerSecond; // ~1s

        const int maxUtteranceSeconds = 30;
        const int maxUtteranceBytes = bytesPerSecond * maxUtteranceSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, stoppingToken);

                foreach (var (guildId, guild) in manager.Guilds)
                {
                    foreach (var (ssrc, buffer) in guild.Buffers)
                    {
                        var buffered = buffer.PeekLength(ssrc);
                        if (buffered == 0)
                        {
                            continue;
                        }

                        var lastAppendUtc = buffer.GetLastAppendUtc(ssrc);
                        var silentFor = lastAppendUtc is null
                            ? TimeSpan.Zero
                            : DateTimeOffset.UtcNow - lastAppendUtc.Value;

                        var shouldFlush =
                            silentFor >= flushAfterSilence || buffered >= maxUtteranceBytes;
                        if (!shouldFlush)
                        {
                            continue;
                        }

                        var data = buffer.SnapshotAndClear(ssrc);
                        if (data.Length < minTranscribeBytes)
                        {
                            continue;
                        }

                        await using var _ = await _transcribeGate.AquireAsync(stoppingToken);

                        logger.LogInformation(
                            "Transcribing voice command from Guild {GuildId} SSRC {Ssrc} ({Length} bytes, ~{Seconds:0.0}s, silent {SilentFor:0.0}s)...",
                            guildId,
                            ssrc,
                            data.Length,
                            (double)data.Length / bytesPerSecond,
                            silentFor.TotalSeconds
                        );

                        var transcript = await transcriber.TranscribeAsync(data, stoppingToken);
                        if (string.IsNullOrWhiteSpace(transcript))
                        {
                            logger.LogInformation(
                                "Transcribed no speech from Guild {GuildId} SSRC {Ssrc}",
                                guildId,
                                ssrc
                            );
                            continue;
                        }

                        logger.LogInformation(
                            "Voice transcript (Guild {GuildId} SSRC {Ssrc}): {Transcript}",
                            guildId,
                            ssrc,
                            transcript
                        );

                        var command = parser.Parse(transcript);
                        if (command.Intent == VoiceCommandIntent.None)
                        {
                            logger.LogInformation("Voice command returned without intent");
                            continue;
                        }

                        logger.LogInformation(
                            "Voice command (Guild {GuildId} SSRC {Ssrc}): {Intent} {Arg}",
                            guildId,
                            ssrc,
                            command.Intent,
                            command.Argument
                        );

                        await dispatcher.DispatchAsync(command, guildId, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Voice command loop crashed; continuing after backoff");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
