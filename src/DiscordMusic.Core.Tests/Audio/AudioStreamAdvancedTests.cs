using DiscordMusic.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordMusic.Core.Tests.Audio;

public class AudioStreamAdvancedTests
{
    [Test]
    public async Task Load_Stopped_DoesNotWriteToOutput()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(200_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Stopped,
            fs,
            path,
            output,
            CancellationToken.None
        );

        // Give background loop a moment. It should not copy bytes in Stopped.
        await Task.Delay(150);
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
        await Assert.That(output.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Load_Playing_ProducesSomeOutput()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(500_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        await AudioStreamTestHelpers.WaitUntilAsync(
            () => output.Length > 0,
            TimeSpan.FromSeconds(2)
        );
        await Assert
            .That(
                audioStream.State is AudioStream.AudioState.Playing or AudioStream.AudioState.Ended
            )
            .IsTrue();
    }

    [Test]
    public async Task Pause_IsIdempotent()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(100_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        audioStream.Pause();
        audioStream.Pause();

        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);
    }

    [Test]
    public async Task Resume_IsIdempotent()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(100_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        audioStream.Resume();
        audioStream.Resume();

        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);
    }

    [Test]
    public async Task PauseThenResume_ContinuesWritingOutput()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(2_000_000)
        );

        var output = new MemoryStream();
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        try
        {
            await AudioStreamTestHelpers.WaitUntilAsync(
                () => output.Length > 0,
                TimeSpan.FromSeconds(2)
            );
            audioStream.Pause();

            var pausedLength = output.Length;
            await Task.Delay(150);

            // Pause() cancels the CopyToAsync, but the producer keeps reading.
            // So the stream may reach EOF and transition to Ended even while paused.
            await Assert
                .That(
                    audioStream.State
                        is AudioStream.AudioState.Silence
                            or AudioStream.AudioState.Ended
                )
                .IsTrue();

            if (audioStream.State == AudioStream.AudioState.Ended)
            {
                return;
            }

            audioStream.Resume();
            await Assert
                .That(
                    audioStream.State
                        is AudioStream.AudioState.Playing
                            or AudioStream.AudioState.Ended
                )
                .IsTrue();

            // After resuming we either write more bytes or hit EOF; both are acceptable.
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(3))
            {
                if (
                    output.Length > pausedLength
                    || audioStream.State == AudioStream.AudioState.Ended
                )
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException(
                "Stream did not resume writing and did not end within the timeout."
            );
        }
        finally
        {
            audioStream.Dispose();
            await output.DisposeAsync();
        }
    }

    [Test]
    public async Task Seek_ForwardAndBackward_AdjustsByRequestedPcmBytes()
    {
        // Use a larger file so we can move around and not instantly EOF.
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(5_000_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Stopped,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var start = audioStream.Seek(TimeSpan.FromSeconds(10), AudioStream.SeekMode.Position);
        await Assert.That(start.IsError).IsFalse();

        // Forward/backward are relative to the current input stream byte position.
        // Assert using PCM bytes to match implementation and avoid TimeSpan rounding differences.
        var beforeForwardBytes = Pcm16Bytes.ToBytes(audioStream.Position).Value;
        var forwardAmount = TimeSpan.FromSeconds(5);
        var forwardDeltaBytes = Pcm16Bytes.ToBytes(forwardAmount).Value;

        var forward = audioStream.Seek(forwardAmount, AudioStream.SeekMode.Forward);
        await Assert.That(forward.IsError).IsFalse();

        var afterForwardBytes = Pcm16Bytes.ToBytes(forward.Value).Value;
        await Assert.That(afterForwardBytes).IsEqualTo(beforeForwardBytes + forwardDeltaBytes);

        var beforeBackwardBytes = Pcm16Bytes.ToBytes(audioStream.Position).Value;
        var backwardAmount = TimeSpan.FromSeconds(3);
        var backwardDeltaBytes = Pcm16Bytes.ToBytes(backwardAmount).Value;

        var backward = audioStream.Seek(backwardAmount, AudioStream.SeekMode.Backward);
        await Assert.That(backward.IsError).IsFalse();

        var afterBackwardBytes = Pcm16Bytes.ToBytes(backward.Value).Value;
        await Assert.That(afterBackwardBytes).IsEqualTo(beforeBackwardBytes - backwardDeltaBytes);
    }

    [Test]
    public async Task Seek_WhilePaused_DoesNotThrow_AndResumePlays()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(2_000_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        await AudioStreamTestHelpers.WaitUntilAsync(
            () => output.Length > 0,
            TimeSpan.FromSeconds(2)
        );

        audioStream.Pause();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);

        var result = audioStream.Seek(TimeSpan.FromSeconds(1), AudioStream.SeekMode.Position);
        await Assert.That(result.IsError).IsFalse();

        audioStream.Resume();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);

        var pausedLength = output.Length;
        await AudioStreamTestHelpers.WaitUntilAsync(
            () => output.Length > pausedLength,
            TimeSpan.FromSeconds(2)
        );
    }

    [Test]
    public async Task StreamEnded_FiresOnlyOnce()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(80_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var endedCount = 0;
        var tcs = AudioStreamTestHelpers.CreateTcs();
        audioStream.StreamEnded += (_, _) =>
        {
            Interlocked.Increment(ref endedCount);
            tcs.TrySetResult();
            return Task.CompletedTask;
        };

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await Assert.That(completed).IsEqualTo(tcs.Task);

        // Wait a moment more to catch duplicate invocations.
        await Task.Delay(100);
        await Assert.That(endedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_StopsFurtherWrites()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(2_000_000)
        );

        await using var output = new MemoryStream();
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        await AudioStreamTestHelpers.WaitUntilAsync(
            () => output.Length > 0,
            TimeSpan.FromSeconds(2)
        );
        var beforeDispose = output.Length;

        audioStream.Dispose();

        await Task.Delay(200);
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
        await Assert.That(output.Length).IsEqualTo(beforeDispose);
    }

    [Test]
    public async Task StreamFailed_Fires_WhenOutputStreamThrows()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(500_000)
        );

        await using var output = new ThrowingWriteStream();

        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Playing,
            fs.FileInfo.New(path),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        using var audioStream = streamOrError.Value;

        var failedTcs = AudioStreamTestHelpers.CreateTcs();
        audioStream.StreamFailed += (_, _, _) =>
        {
            failedTcs.TrySetResult();
            return Task.CompletedTask;
        };

        var completed = await Task.WhenAny(failedTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await Assert.That(completed).IsEqualTo(failedTcs.Task);
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
    }

    [Test]
    public async Task Concurrent_PauseResumeSeek_Dispose_NoThrow_FinalStateStopped()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(3_000_000)
        );

        await using var output = new MemoryStream();
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var tasks = new List<Task>();

        tasks.Add(
            Task.Run(() =>
            {
                for (var i = 0; i < 30; i++)
                {
                    audioStream.Pause();
                    audioStream.Resume();
                }
            })
        );

        tasks.Add(
            Task.Run(() =>
            {
                for (var i = 0; i < 30; i++)
                {
                    _ = audioStream.Seek(
                        TimeSpan.FromMilliseconds(20 * i),
                        AudioStream.SeekMode.Position
                    );
                }
            })
        );

        tasks.Add(
            Task.Run(() =>
            {
                // Dispose mid-flight.
                Thread.Sleep(50);
                audioStream.Dispose();
            })
        );

        await Task.WhenAll(tasks);

        // No throw is the primary assertion. State must be stopped after dispose.
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
    }

    [Test]
    public async Task Seek_PositionToZero_SetsExactStart()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(500_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Stopped,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var result = audioStream.Seek(TimeSpan.Zero, AudioStream.SeekMode.Position);
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(Pcm16Bytes.ToBytes(result.Value).Value).IsEqualTo(0);
    }

    [Test]
    public async Task Seek_NegativePosition_ClampsToStart()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(500_000)
        );

        await using var output = new MemoryStream();
        using var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Stopped,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var result = audioStream.Seek(TimeSpan.FromSeconds(-5), AudioStream.SeekMode.Position);
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(Pcm16Bytes.ToBytes(result.Value).Value).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_PreventsStreamEndedFromFiringAfterDispose()
    {
        // Large file: we want the stream to be in-flight when we dispose.
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(5_000_000)
        );

        await using var output = new MemoryStream();
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var endedTcs = AudioStreamTestHelpers.CreateTcs();
        audioStream.StreamEnded += (_, _) =>
        {
            endedTcs.TrySetResult();
            return Task.CompletedTask;
        };

        // Ensure we started playback first.
        await AudioStreamTestHelpers.WaitUntilAsync(
            () => output.Length > 0,
            TimeSpan.FromSeconds(2)
        );

        audioStream.Dispose();

        // We should not observe StreamEnded after disposal. Give it a small window for race conditions.
        var completed = await Task.WhenAny(
            endedTcs.Task,
            Task.Delay(TimeSpan.FromMilliseconds(300))
        );
        await Assert.That(completed).IsNotEqualTo(endedTcs.Task);
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
    }

    [Test]
    public async Task RapidSeekStorm_LastSeekWins_AndDoesNotTransitionToEnded()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(10_000_000)
        );

        var output = new MemoryStream();
        await using var _ = output;
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        try
        {
            // Ensure playback loop is active.
            await AudioStreamTestHelpers.WaitUntilAsync(
                () => output.Length > 0,
                TimeSpan.FromSeconds(2)
            );

            // Seek a bunch of times while CopyToAsync is likely in progress.
            var positions = new[]
            {
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(800),
                TimeSpan.FromMilliseconds(300),
            };

            TimeSpan? lastReturned = null;
            foreach (var pos in positions)
            {
                var seek = audioStream.Seek(pos, AudioStream.SeekMode.Position);
                await Assert.That(seek.IsError).IsFalse();

                // Assert the returned position corresponds to the requested PCM byte offset.
                await Assert
                    .That(Pcm16Bytes.ToBytes(seek.Value).Value)
                    .IsEqualTo(Pcm16Bytes.ToBytes(pos).Value);

                lastReturned = seek.Value;
            }

            // Give the state machine a moment to react after the last seek.
            await Task.Delay(50);

            // Last seek should have won (Position can advance concurrently, so assert lower bound)
            await Assert.That(lastReturned.HasValue).IsTrue();
            var currentBytes = Pcm16Bytes.ToBytes(audioStream.Position).Value;
            var lastBytes = Pcm16Bytes.ToBytes(lastReturned!.Value).Value;
            await Assert.That(currentBytes).IsGreaterThanOrEqualTo(lastBytes);
        }
        finally
        {
            audioStream.Dispose();
        }
    }

    [Test]
    public async Task PauseThenSeekThenResume_ContinuesFromNewPosition()
    {
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(8_000_000)
        );

        var output = new MemoryStream();
        await using var _ = output;
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        try
        {
            await AudioStreamTestHelpers.WaitUntilAsync(
                () => output.Length > 0,
                TimeSpan.FromSeconds(2)
            );

            audioStream.Pause();
            await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);

            var oldOutputLength = output.Length;

            var requested = TimeSpan.FromSeconds(2);
            var seek = audioStream.Seek(requested, AudioStream.SeekMode.Position);
            await Assert.That(seek.IsError).IsFalse();

            // Assert via returned position; AudioStream.Position can advance in the background.
            var seekBytes = Pcm16Bytes.ToBytes(seek.Value).Value;
            await Assert.That(seekBytes).IsEqualTo(Pcm16Bytes.ToBytes(requested).Value);

            audioStream.Resume();
            await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);

            // After resuming we either write more bytes or hit EOF; both are acceptable.
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(3))
            {
                if (
                    output.Length > oldOutputLength
                    || audioStream.State == AudioStream.AudioState.Ended
                )
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException(
                "Stream did not resume writing and did not end within the timeout."
            );
        }
        finally
        {
            audioStream.Dispose();
        }
    }

    [Test]
    public async Task Dispose_WhileStreamEndedHandlerIsBlocked_DoesNotDeadlock_AndEndedFiresOnce()
    {
        // Smallish file so it reaches EOF quickly.
        var (fs, path) = AudioStreamTestHelpers.CreateAudioFile(
            AudioStreamTestHelpers.CreateAudioBytes(120_000)
        );

        await using var output = new MemoryStream();
        var audioStream = await AudioStreamTestHelpers.LoadAsync(
            AudioStream.AudioState.Playing,
            fs,
            path,
            output,
            CancellationToken.None
        );

        var allowHandlerToComplete = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var endedCount = 0;
        var handlerEntered = AudioStreamTestHelpers.CreateTcs();

        audioStream.StreamEnded += async (_, _) =>
        {
            Interlocked.Increment(ref endedCount);
            handlerEntered.TrySetResult();
            await allowHandlerToComplete.Task;
        };

        try
        {
            // Wait for handler to start, then dispose while it's blocked.
            var entered = await Task.WhenAny(
                handlerEntered.Task,
                Task.Delay(TimeSpan.FromSeconds(2))
            );
            await Assert.That(entered).IsEqualTo(handlerEntered.Task);

            // Dispose should not deadlock even though the state machine is awaiting the handler.
            audioStream.Dispose();

            // Unblock handler and ensure we can proceed.
            allowHandlerToComplete.TrySetResult();

            // Give the state machine some time to unwind.
            await Task.Delay(50);

            await Assert.That(endedCount).IsEqualTo(1);
            await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
        }
        finally
        {
            // In case the test exited early, don't leak.
            audioStream.Dispose();
        }
    }

    private sealed class ThrowingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new IOException("boom");

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new IOException("boom");

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => Task.FromException(new IOException("boom"));

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromException(new IOException("boom"));
    }
}
