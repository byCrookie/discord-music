using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Audio.Sources;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Audio.Sending;

internal sealed class TimedAudioSender(IPcmAudioSourceFactory audioSourceFactory) : IAudioSender
{
    public static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(20);
    private const int SampleRate = 48_000;
    private const int Channels = 2;
    private const int BytesPerSample = sizeof(float);
    public const int FrameSizeBytes = SampleRate / 1000 * 20 * Channels * BytesPerSample;
    private const int BytesPerSecond = SampleRate * Channels * BytesPerSample;

    public async Task SendAsync(
        VoiceClient voiceClient,
        Track track,
        IFileInfo inputFile,
        TimeSpan startPosition,
        PlaybackSession playbackSession,
        CancellationToken cancellationToken
    )
    {
        await voiceClient.EnterSpeakingStateAsync(
            new SpeakingProperties(SpeakingFlags.Microphone),
            cancellationToken: cancellationToken
        );

        await using var voiceStream = voiceClient.CreateVoiceStream();
        await using var opusEncodeStream = new OpusEncodeStream(
            voiceStream,
            PcmFormat.Float,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );
        await using var input = await audioSourceFactory.OpenAsync(
            inputFile,
            startPosition,
            cancellationToken
        );

        var frame = new byte[FrameSizeBytes];
        var position = startPosition;
        var stopwatch = Stopwatch.StartNew();
        var nextFrameAt = TimeSpan.Zero;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await playbackSession.WaitWhilePausedAsync(cancellationToken))
            {
                stopwatch.Restart();
                nextFrameAt = TimeSpan.Zero;
            }

            var bytesRead = await ReadFrameAsync(input, frame, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await opusEncodeStream.WriteAsync(frame.AsMemory(0, FrameSizeBytes), cancellationToken);

            position += FrameDuration;
            playbackSession.UpdatePosition(position);

            nextFrameAt += FrameDuration;
            var delay = nextFrameAt - stopwatch.Elapsed;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
            else if (-delay > TimeSpan.FromMilliseconds(100))
            {
                stopwatch.Restart();
                nextFrameAt = TimeSpan.Zero;
            }
        }

        await opusEncodeStream.FlushAsync(cancellationToken);
    }

    internal static long CalculateByteOffset(TimeSpan position)
    {
        if (position <= TimeSpan.Zero)
        {
            return 0;
        }

        var offset = (long)(position.TotalSeconds * BytesPerSecond);
        return offset - offset % FrameSizeBytes;
    }

    private static async ValueTask<int> ReadFrameAsync(
        Stream input,
        byte[] frame,
        CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        while (totalRead < frame.Length)
        {
            var read = await input.ReadAsync(
                frame.AsMemory(totalRead, frame.Length - totalRead),
                cancellationToken
            );
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead > 0 && totalRead < frame.Length)
        {
            Array.Clear(frame, totalRead, frame.Length - totalRead);
        }

        return totalRead;
    }
}
