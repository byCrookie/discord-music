using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Abstractions;
using DiscordMusic.Core.Audio.Sources;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Audio.Sending;

internal sealed class TimedAudioSender(
    IPcmAudioSourceFactory audioSourceFactory,
    TimeProvider timeProvider
) : IAudioSender
{
    public static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(20);
    private const int SampleRate = 48_000;
    private const int Channels = 2;
    private const int BytesPerSample = sizeof(float);
    public const int FrameSizeBytes = SampleRate / 1000 * 20 * Channels * BytesPerSample;
    private const int BytesPerSecond = SampleRate * Channels * BytesPerSample;
    private static readonly Counter<long> AudioFramesSent =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.audio.frames.sent",
            unit: "1",
            description: "Audio frames written to the Discord voice stream."
        );
    private static readonly Histogram<double> AudioFrameLag =
        DiscordMusicObservability.Meter.CreateHistogram<double>(
            "discord.music.audio.frame.lag",
            unit: "s",
            description: "Observed audio frame scheduling lag."
        );
    private static readonly Counter<long> AudioSendOperations =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.audio.send.operations",
            unit: "1",
            description: "Audio send operations."
        );

    public async Task SendAsync(
        ulong guildId,
        VoiceClient voiceClient,
        Track track,
        IFileInfo inputFile,
        TimeSpan startPosition,
        PlaybackSession playbackSession,
        CancellationToken cancellationToken
    )
    {
        var metricTags = DiscordMusicObservability.GuildTags(guildId);
        var result = "completed";
        using var activity = DiscordMusicObservability.StartActivity(
            "audio.send",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetGuildTag(activity, guildId);
        DiscordMusicObservability.SetTag(activity, "music.track.id", track.Id);
        DiscordMusicObservability.SetTag(
            activity,
            "music.playback.start_position_ms",
            startPosition.TotalMilliseconds
        );

        try
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
            var startedAt = timeProvider.GetTimestamp();
            var nextFrameAt = TimeSpan.Zero;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await playbackSession.WaitWhilePausedAsync(cancellationToken))
                {
                    startedAt = timeProvider.GetTimestamp();
                    nextFrameAt = TimeSpan.Zero;
                }

                var bytesRead = await ReadFrameAsync(input, frame, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                await opusEncodeStream.WriteAsync(
                    frame.AsMemory(0, FrameSizeBytes),
                    cancellationToken
                );
                AudioFramesSent.Add(1, metricTags);

                position += FrameDuration;
                playbackSession.UpdatePosition(position);

                nextFrameAt += FrameDuration;
                var delay = nextFrameAt - timeProvider.GetElapsedTime(startedAt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, cancellationToken);
                }
                else if (-delay > TimeSpan.FromMilliseconds(100))
                {
                    AudioFrameLag.Record((-delay).TotalSeconds, metricTags);
                    startedAt = timeProvider.GetTimestamp();
                    nextFrameAt = TimeSpan.Zero;
                }
            }

            await opusEncodeStream.FlushAsync(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException)
        {
            result = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Ok, result);
            throw;
        }
        catch (Exception ex)
        {
            result = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            DiscordMusicObservability.SetTag(activity, "result", result);
            var operationTags = DiscordMusicObservability.GuildTags(guildId);
            operationTags.Add("result", result);
            AudioSendOperations.Add(1, operationTags);
        }
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
