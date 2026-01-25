using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandService(
    ILogger<VoiceCommandService> logger,
    IVoiceTranscriber transcriber,
    IVoiceCommandParser parser,
    VoiceCommandDispatcher dispatcher,
    VoiceCommandManager manager
) : BackgroundService
{
    // Global gate: only one transcription (and command dispatch) at a time.
    private readonly SemaphoreSlim _transcribeGate = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(250);

        var bytesPerSecond = 16000 * 2;
        var flushAfterSilence = TimeSpan.FromSeconds(1);
        var minTranscribeBytes = bytesPerSecond; // ~1s

        var maxUtteranceSeconds = 30;
        var maxUtteranceBytes = bytesPerSecond * maxUtteranceSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, stoppingToken);

                foreach (var guildPair in manager.Guilds)
                {
                    var guildId = guildPair.Key;
                    var guild = guildPair.Value;

                    foreach (var bufferPair in guild.Buffers)
                    {
                        var ssrc = bufferPair.Key;
                        var buffer = bufferPair.Value;

                        var buffered = buffer.PeekLength(ssrc);
                        if (buffered == 0)
                            continue;

                        var lastAppendUtc = buffer.GetLastAppendUtc(ssrc);
                        var silentFor =
                            lastAppendUtc is null
                                ? TimeSpan.Zero
                                : (DateTimeOffset.UtcNow - lastAppendUtc.Value);

                        var shouldFlush = silentFor >= flushAfterSilence || buffered >= maxUtteranceBytes;
                        if (!shouldFlush)
                            continue;

                        var data = buffer.SnapshotAndClear(ssrc);
                        if (data.Length < minTranscribeBytes)
                            continue;

                        await _transcribeGate.WaitAsync(stoppingToken);
                        try
                        {
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
                                logger.LogInformation("Transcribed no speech from Guild {GuildId} SSRC {Ssrc}", guildId, ssrc);
                                continue;
                            }

                            logger.LogInformation("Voice transcript (Guild {GuildId} SSRC {Ssrc}): {Transcript}", guildId, ssrc, transcript);

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
                        finally
                        {
                            _transcribeGate.Release();
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Voice command loop crashed; continuing");
            }
        }
    }

    public override void Dispose()
    {
        _transcribeGate.Dispose();
        base.Dispose();
    }
}
