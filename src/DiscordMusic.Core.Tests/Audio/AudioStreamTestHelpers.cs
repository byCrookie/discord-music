using DiscordMusic.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Audio;

internal static class AudioStreamTestHelpers
{
    internal static (MockFileSystem fs, string path) CreateAudioFile(byte[] bytes)
    {
        const string tempFile = "tempAudioFile";
        var fs = new MockFileSystem();
        fs.Initialize().WithFile(tempFile).Which(f => f.HasBytesContent(bytes));
        return (fs, tempFile);
    }

    internal static byte[] CreateAudioBytes(int length, byte fill = 0x2A)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, fill);
        return bytes;
    }

    internal static async Task WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        var started = DateTime.UtcNow;

        while (!predicate())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(interval);
        }
    }

    internal static TaskCompletionSource CreateTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task<AudioStream> LoadAsync(
        AudioStream.AudioState initialState,
        MockFileSystem fs,
        string path,
        Stream output,
        CancellationToken ct = default)
    {
        var streamOrError = AudioStream.Load(
            initialState,
            fs.FileInfo.New(path),
            output,
            fs,
            NullLogger.Instance,
            ct
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        return streamOrError.Value;
    }
}
