using DiscordMusic.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Audio;

public class AudioStreamLifecycleTests
{
    private static (MockFileSystem fs, string path) CreateAudioFile(byte[] bytes)
    {
        const string tempFile = "tempAudioFile";
        var fs = new MockFileSystem();
        fs.Initialize().WithFile(tempFile).Which(f => f.HasBytesContent(bytes));
        return (fs, tempFile);
    }

    [Test]
    public async Task Load_ReturnsNotFound_WhenAudioFileDoesNotExist()
    {
        var fs = new MockFileSystem();
        fs.Initialize();

        await using var output = new MemoryStream();

        var result = AudioStream.Load(
            AudioStream.AudioState.Stopped,
            fs.FileInfo.New("missing"),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.FirstError.Type).IsEqualTo(ErrorOr.ErrorType.NotFound);
    }

    [Test]
    public async Task Seek_ReturnsError_WhenDisposed()
    {
        var (fs, path) = CreateAudioFile(new byte[100]);

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Stopped,
            fs.FileInfo.New(path),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        var audioStream = streamOrError.Value;
        audioStream.Dispose();

        var seekResult = audioStream.Seek(TimeSpan.FromSeconds(1), AudioStream.SeekMode.Position);
        await Assert.That(seekResult.IsError).IsTrue();
    }

    [Test]
    public async Task StreamEnded_Fires_WhenPlayingReachesEnd()
    {
        // Small file is fine: FillPipe will complete quickly and CopyToAsync will end.
        var (fs, path) = CreateAudioFile(new byte[50_000]);

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Stopped,
            fs.FileInfo.New(path),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        using var audioStream = streamOrError.Value;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        audioStream.StreamEnded += (_, _) =>
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        };

        // Start playback only after we've subscribed, to avoid missing the event on fast machines.
        audioStream.Resume();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(completed == tcs.Task).IsTrue();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Ended);

        await Assert.That(output.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Pause_DoesNotEndStream_AndResumeContinues()
    {
        // Pause() cancels the active CopyToAsync, but the producer keeps reading from disk.
        // So the stream may reach EOF (and fire StreamEnded) even while paused.
        var (fs, path) = CreateAudioFile(new byte[200_000]);

        await using var output = new MemoryStream();
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

        var endedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        audioStream.StreamEnded += (_, _) =>
        {
            endedTcs.TrySetResult();
            return Task.CompletedTask;
        };

        audioStream.Pause();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);

        // Give the state machine a moment; it must stay paused.
        await Task.Delay(100);
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);

        // If it hasn't ended already, resume playing.
        if (!endedTcs.Task.IsCompleted)
        {
            audioStream.Resume();
            await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);
        }

        // It must eventually end (either while paused or after resume).
        var completed = await Task.WhenAny(endedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(completed == endedTcs.Task).IsTrue();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Ended);
    }

    [Test]
    public async Task Dispose_StopsStream_AndDoesNotThrow_WhenCalledTwice()
    {
        var (fs, path) = CreateAudioFile(new byte[500_000]);

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Playing,
            fs.FileInfo.New(path),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        var audioStream = streamOrError.Value;

        audioStream.Dispose();
        audioStream.Dispose();

        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Stopped);
    }
}
