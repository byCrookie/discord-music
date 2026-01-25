using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.VoiceCommands;

/// <summary>
/// Injectable, non-hosted component that owns per-guild voice buffers and subscription wiring.
/// The hosted background service processes the buffers.
/// </summary>
internal sealed class VoiceCommandManager : IVoiceCommandService
{
    internal sealed class GuildState
    {
        public ConcurrentDictionary<uint, VoiceCommandBuffer> Buffers { get; } = new();

        // OpusDecoder/buffer reuse is guarded because VoiceReceive can be concurrent.
        public Lock DecodeLock { get; } = new();
        public OpusDecoder OpusDecoder { get; } = new(VoiceChannels.Stereo);
        public byte[] Pcm48KStereo { get; } = new byte[Opus.GetFrameSize(PcmFormat.Short, VoiceChannels.Stereo)];
        public short[] Mono48KSamples { get; } = new short[Opus.SamplesPerChannel];
        public short[] Mono16KSamples { get; } = new short[Opus.SamplesPerChannel / 3];
    }

    private readonly ILogger<VoiceCommandManager> _logger;
    private readonly ConcurrentDictionary<ulong, GuildState> _guilds = new();

    public VoiceCommandManager(ILogger<VoiceCommandManager> logger)
    {
        _logger = logger;
    }

    internal IReadOnlyDictionary<ulong, GuildState> Guilds => _guilds;

    public IDisposable Subscribe(ulong guildId, VoiceClient voiceClient)
    {
        var guild = _guilds.GetOrAdd(guildId, _ => new GuildState());

        ValueTask Handler(VoiceReceiveEventArgs args)
        {
            try
            {
                lock (guild.DecodeLock)
                {
                    guild.OpusDecoder.Decode(args.Frame, guild.Pcm48KStereo);

                    DownmixStereoS16ToMono(
                        MemoryMarshal.Cast<byte, short>(guild.Pcm48KStereo.AsSpan()),
                        guild.Mono48KSamples
                    );

                    Resample48KTo16KBy3(guild.Mono48KSamples, guild.Mono16KSamples);

                    var buffer = guild.Buffers.GetOrAdd(args.Ssrc, _ => new VoiceCommandBuffer());
                    buffer.Append(args.Ssrc, MemoryMarshal.AsBytes(guild.Mono16KSamples.AsSpan()));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed decoding/buffering voice frame");
            }

            return ValueTask.CompletedTask;
        }

        voiceClient.VoiceReceive += Handler;
        return new Subscription(() => voiceClient.VoiceReceive -= Handler);
    }

    public void UnsubscribeGuild(ulong guildId)
    {
        if (_guilds.TryRemove(guildId, out var state))
        {
            state.OpusDecoder.Dispose();
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

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
