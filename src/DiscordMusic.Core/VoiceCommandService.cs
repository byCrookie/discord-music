using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.VoiceCommands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core;

internal sealed class VoiceCommandService(
    ILogger<VoiceCommandService> logger,
    IVoiceTranscriber transcriber,
    IVoiceCommandParser parser,
    VoiceCommandDispatcher dispatcher
) : BackgroundService
{
    private sealed record Key(ulong GuildId, uint Ssrc);

    private sealed class PerKey
    {
        public VoiceCommandBuffer Buffer { get; } = new();
    }

    private readonly ConcurrentDictionary<Key, PerKey> _buffers = new();
    private readonly ConcurrentDictionary<ulong, byte> _subscribedGuilds = new();

    private readonly Lock _decodeLock = new();
    private readonly OpusDecoder _opusDecoder = new(VoiceChannels.Stereo);
    private readonly byte[] _pcm48KStereo = new byte[Opus.GetFrameSize(PcmFormat.Short, VoiceChannels.Stereo)];
    private readonly short[] _mono48KSamples = new short[Opus.SamplesPerChannel];
    private readonly short[] _mono16KSamples = new short[Opus.SamplesPerChannel / 3];

    internal void Subscribe(GuildSession session)
    {
        if (!_subscribedGuilds.TryAdd(session.Guild.Id, 0))
            return;

        session.GuildVoiceSession.VoiceClient.VoiceReceive += args =>
        {
            try
            {
                lock (_decodeLock)
                {
                    _opusDecoder.Decode(args.Frame, _pcm48KStereo);

                    DownmixStereoS16ToMono(
                        MemoryMarshal.Cast<byte, short>(_pcm48KStereo.AsSpan()),
                        _mono48KSamples
                    );

                    Resample48KTo16KBy3(_mono48KSamples, _mono16KSamples);

                    var key = new Key(session.Guild.Id, args.Ssrc);
                    var entry = _buffers.GetOrAdd(key, _ => new PerKey());
                    entry.Buffer.Append(args.Ssrc, MemoryMarshal.AsBytes(_mono16KSamples.AsSpan()));
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed decoding/buffering voice frame");
            }

            return ValueTask.CompletedTask;
        };
    }

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

                foreach (var pair in _buffers)
                {
                    var key = pair.Key;
                    var buffer = pair.Value.Buffer;

                    var buffered = buffer.PeekLength(key.Ssrc);
                    if (buffered == 0)
                        continue;

                    var lastAppendUtc = buffer.GetLastAppendUtc(key.Ssrc);
                    var silentFor =
                        lastAppendUtc is null
                            ? TimeSpan.Zero
                            : (DateTimeOffset.UtcNow - lastAppendUtc.Value);

                    var shouldFlush = silentFor >= flushAfterSilence || buffered >= maxUtteranceBytes;
                    if (!shouldFlush)
                        continue;

                    var data = buffer.SnapshotAndClear(key.Ssrc);
                    if (data.Length < minTranscribeBytes)
                        continue;

                    logger.LogInformation(
                        "Transcribing voice command from Guild {GuildId} SSRC {Ssrc} ({Length} bytes, ~{Seconds:0.0}s, silent {SilentFor:0.0}s)...",
                        key.GuildId,
                        key.Ssrc,
                        data.Length,
                        (double)data.Length / bytesPerSecond,
                        silentFor.TotalSeconds
                    );

                    var transcript = await transcriber.TranscribeAsync(data, stoppingToken);
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        logger.LogInformation("Transcribed no speech from Guild {GuildId} SSRC {Ssrc}", key.GuildId, key.Ssrc);
                        continue;
                    }

                    logger.LogInformation("Voice transcript (Guild {GuildId} SSRC {Ssrc}): {Transcript}", key.GuildId, key.Ssrc, transcript);

                    var command = parser.Parse(transcript);
                    if (command.Intent == VoiceCommandIntent.None)
                    {
                        logger.LogInformation("Voice command returned without intent");
                        continue;
                    }

                    logger.LogInformation(
                        "Voice command (Guild {GuildId} SSRC {Ssrc}): {Intent} {Arg}",
                        key.GuildId,
                        key.Ssrc,
                        command.Intent,
                        command.Argument
                    );

                    await dispatcher.DispatchAsync(command, key.GuildId, stoppingToken);
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
        var samples = monoOut.Length;
        for (var i = 0; i < samples; i++)
        {
            var l = stereoInterleaved[i * 2];
            var r = stereoInterleaved[i * 2 + 1];
            monoOut[i] = (short)((l + r) / 2);
        }
    }

    private static void Resample48KTo16KBy3(ReadOnlySpan<short> mono48K, Span<short> mono16KOut)
    {
        var outSamples = Math.Min(mono16KOut.Length, mono48K.Length / 3);
        for (var i = 0; i < outSamples; i++)
        {
            mono16KOut[i] = mono48K[i * 3];
        }
    }

    public override void Dispose()
    {
        _opusDecoder.Dispose();
        base.Dispose();
    }
}
