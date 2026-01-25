using System.Runtime.InteropServices;
using DiscordMusic.Core.VoiceCommands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core;

public sealed class VoiceCommandService(
    ILogger<VoiceCommandService> logger,
    IVoiceHost voiceHost,
    IVoiceTranscriber transcriber,
    IVoiceCommandParser parser,
    VoiceCommandDispatcher dispatcher
) : BackgroundService
{
    private readonly VoiceCommandBuffer _buffer = new();
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Decoder: Discord voice is Opus at 48kHz.
        // We'll decode Opus -> PCM s16 @48k stereo, downmix to mono, then resample to 16k mono for Whisper.
        using var opusDecoder = new OpusDecoder(VoiceChannels.Stereo);

        var pcm48kStereoFrameBytes = Opus.GetFrameSize(PcmFormat.Short, VoiceChannels.Stereo); // 20ms
        var pcm48kStereo = new byte[pcm48kStereoFrameBytes];

        // 48kHz -> 16kHz decimation by 3
        // 20ms @48k => 960 samples/channel. Stereo => 1920 samples total.
        // After downmix mono => 960 mono samples. After /3 => 320 mono samples @16k.
        var mono48kSamples = new short[Opus.SamplesPerChannel];
        var mono16kSamples = new short[Opus.SamplesPerChannel / 3];

        // Receive frames and append them for later transcription.
        voiceHost.VoiceReceive += args =>
        {
            // if (voiceHost.VoiceClient?.Status == WebSocketStatus.Ready)
            // {
            //     voiceHost.VoiceConnection?.VoiceOutStream.Write(args.Frame);
            // }

            try
            {
                // args.Frame is an Opus packet. Decode to PCM first.
                // NetCord's decoder decodes a single Opus packet into a fixed 20ms PCM buffer.
                opusDecoder.Decode(args.Frame, pcm48kStereo);

                // Downmix the decoded stereo PCM to mono @48k.
                DownmixStereoS16ToMono(
                    MemoryMarshal.Cast<byte, short>(pcm48kStereo.AsSpan()),
                    mono48kSamples
                );

                // Resample 48k -> 16k by simple decimation (every 3rd sample).
                Resample48kTo16kBy3(mono48kSamples, mono16kSamples);

                // Append PCM 16k mono s16 to per-user buffer.
                _buffer.Append(args.Ssrc, MemoryMarshal.AsBytes(mono16kSamples.AsSpan()));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed decoding/buffering voice frame");
            }

            return default;
        };

        // Per-SSRC utterance detector:
        // - keep buffering while frames keep arriving
        // - flush/transcribe only after >= 2s without any new frames for that SSRC
        // - safety cap to avoid unbounded buffers if silence is never detected
        var pollInterval = TimeSpan.FromMilliseconds(250);

        // Now we buffer 16kHz mono s16.
        var bytesPerSecond = 16000 * 2;

        var flushAfterSilence = TimeSpan.FromSeconds(1);
        var minTranscribeBytes = bytesPerSecond; // ~1s

        // Safety cap: force flush at ~30s worth of audio even if we don't see a silence gap.
        var maxUtteranceSeconds = 30;
        var maxUtteranceBytes = bytesPerSecond * maxUtteranceSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, stoppingToken);

                if (voiceHost.VoiceClient?.Status != WebSocketStatus.Ready)
                    continue;

                // We buffer by SSRC, so enumerate the SSRCs we actually have audio for.
                foreach (var ssrc in _buffer.GetSsrcs())
                {
                    var buffered = _buffer.PeekLength(ssrc);
                    if (buffered == 0)
                        continue;

                    var lastAppendUtc = _buffer.GetLastAppendUtc(ssrc);
                    var silentFor =
                        lastAppendUtc is null
                            ? TimeSpan.Zero
                            : (DateTimeOffset.UtcNow - lastAppendUtc.Value);

                    var shouldFlush = silentFor >= flushAfterSilence || buffered >= maxUtteranceBytes;
                    if (!shouldFlush)
                        continue;

                    var data = _buffer.SnapshotAndClear(ssrc);
                    if (data.Length < minTranscribeBytes)
                        continue;

                    logger.LogInformation(
                        "Transcribing voice command from SSRC {Ssrc} ({Length} bytes, ~{Seconds:0.0}s, silent {SilentFor:0.0}s)...",
                        ssrc,
                        data.Length,
                        (double)data.Length / bytesPerSecond,
                        silentFor.TotalSeconds
                    );

                    var transcript = await transcriber.TranscribeAsync(data, stoppingToken);
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        logger.LogInformation("Transcribed no speech from SSRC {Ssrc}", ssrc);
                        continue;
                    }

                    logger.LogInformation("Voice transcript ({Ssrc}): {Transcript}", ssrc, transcript);

                    var command = parser.Parse(transcript);
                    if (command.Intent == VoiceCommandIntent.None)
                    {
                        logger.LogInformation("Voice command returned without intent");
                        continue;
                    }

                    logger.LogInformation(
                        "Voice command ({Ssrc}): {Intent} {Arg}",
                        ssrc,
                        command.Intent,
                        command.Argument
                    );

                    await dispatcher.DispatchAsync(command, new VoiceHostContext(voiceHost
                        .VoiceClient.UserId, voiceHost
                        .VoiceClient.GuildId, voiceHost.VoiceConnection!.ChannelId), stoppingToken);
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

    private static void DownmixStereoS16ToMono(ReadOnlySpan<short> stereoInterleaved, Span<short> monoOut)
    {
        // stereoInterleaved length should be monoOut.Length * 2
        var samples = monoOut.Length;
        for (var i = 0; i < samples; i++)
        {
            var l = stereoInterleaved[i * 2];
            var r = stereoInterleaved[i * 2 + 1];
            monoOut[i] = (short)((l + r) / 2);
        }
    }

    private static void Resample48kTo16kBy3(ReadOnlySpan<short> mono48k, Span<short> mono16kOut)
    {
        // Simple decimation: take every 3rd sample.
        // NOTE: This is not a high-quality resampler; it’s a pragmatic baseline.
        var outSamples = Math.Min(mono16kOut.Length, mono48k.Length / 3);
        for (var i = 0; i < outSamples; i++)
        {
            mono16kOut[i] = mono48k[i * 3];
        }
    }
}
