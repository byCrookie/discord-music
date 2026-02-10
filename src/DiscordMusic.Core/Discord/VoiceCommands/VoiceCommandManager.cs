using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandManager(ILogger<VoiceCommandManager> logger)
{
    internal sealed class GuildState
    {
        // 60ms frames: lower CPU/bandwidth, slightly higher latency.
        private const float FrameDurationMs = 60;
        public static readonly int SamplesPerChannel = Opus.GetSamplesPerChannel(FrameDurationMs);

        public ConcurrentDictionary<uint, VoiceCommandBuffer> Buffers { get; } = new();

        // OpusDecoder/buffer reuse is guarded because VoiceReceive can be concurrent.
        public Lock DecodeLock { get; } = new();
        public OpusDecoder OpusDecoder { get; } = new(VoiceChannels.Stereo);

        public short[] Pcm48KStereo { get; } = new short[SamplesPerChannel * 2];
        public short[] Mono48KSamples { get; } = new short[SamplesPerChannel];
        public short[] Mono16KSamples { get; } = new short[SamplesPerChannel / 3];
    }

    private readonly ConcurrentDictionary<ulong, GuildState> _guilds = new();

    internal IReadOnlyDictionary<ulong, GuildState> Guilds => _guilds;

    public IDisposable Subscribe(ulong guildId, VoiceClient voiceClient)
    {
        var guild = _guilds.GetOrAdd(guildId, _ => new GuildState());

        voiceClient.VoiceReceive += Handler;
        return new Subscription(() => voiceClient.VoiceReceive -= Handler);

        ValueTask Handler(VoiceReceiveEventArgs args)
        {
            try
            {
                lock (guild.DecodeLock)
                {
                    var decodedSamplesPerChannel = guild.OpusDecoder.Decode(
                        args.Frame,
                        guild.Pcm48KStereo,
                        GuildState.SamplesPerChannel
                    );

                    if (decodedSamplesPerChannel <= 0)
                    {
                        return ValueTask.CompletedTask;
                    }

                    var totalStereoSamples = decodedSamplesPerChannel * 2;

                    DownmixStereoS16ToMono(
                        guild.Pcm48KStereo.AsSpan(0, totalStereoSamples),
                        guild.Mono48KSamples.AsSpan(0, decodedSamplesPerChannel)
                    );

                    var outSamples = Resample48KTo16KBy3(
                        guild.Mono48KSamples.AsSpan(0, decodedSamplesPerChannel),
                        guild.Mono16KSamples
                    );

                    var buffer = guild.Buffers.GetOrAdd(args.Ssrc, _ => new VoiceCommandBuffer());
                    buffer.Append(
                        args.Ssrc,
                        MemoryMarshal.AsBytes(guild.Mono16KSamples.AsSpan(0, outSamples))
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed decoding/buffering voice frame. GuildId={GuildId} SSRC={Ssrc} FrameLength={FrameLength}",
                    guildId,
                    args.Ssrc,
                    args.Frame.Length
                );
            }

            return ValueTask.CompletedTask;
        }
    }

    public void UnsubscribeGuild(ulong guildId)
    {
        if (_guilds.TryRemove(guildId, out var state))
        {
            state.OpusDecoder.Dispose();
        }
    }

    private static void DownmixStereoS16ToMono(
        ReadOnlySpan<short> stereoInterleaved,
        Span<short> monoOut
    )
    {
        var samples = monoOut.Length;
        for (var i = 0; i < samples; i++)
        {
            var l = stereoInterleaved[i * 2];
            var r = stereoInterleaved[i * 2 + 1];
            monoOut[i] = (short)((l + r) / 2);
        }
    }

    private static int Resample48KTo16KBy3(ReadOnlySpan<short> mono48K, Span<short> mono16KOut)
    {
        var outSamples = Math.Min(mono16KOut.Length, mono48K.Length / 3);
        for (var i = 0; i < outSamples; i++)
        {
            mono16KOut[i] = mono48K[i * 3];
        }

        return outSamples;
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
