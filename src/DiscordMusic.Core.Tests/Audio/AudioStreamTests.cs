using DiscordMusic.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;


namespace DiscordMusic.Core.Tests.Audio;

public class AudioStreamTests
{
    [Test]
    public async Task Seek_ClampsToStart()
    {
        const string tempFile = "tempAudioFile";
        
        var fs = new MockFileSystem();
        fs.Initialize()
            .WithFile(tempFile)
            .Which(f => f.HasBytesContent(new byte[100]));

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Stopped,
            fs.FileInfo.New(tempFile),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        using var audioStream = streamOrError.Value;

        var result = audioStream.Seek(TimeSpan.FromSeconds(999), AudioStream.SeekMode.Backward);
        await Assert.That(result.IsError).IsFalse();

        // Assert via the returned clamped position; AudioStream.Position can advance in the background.
        await Assert.That(Pcm16Bytes.ToBytes(result.Value).Value).IsEqualTo(0);
    }

    [Test]
    public async Task Seek_ClampsToEnd()
    {
        const string tempFile = "tempAudioFile";
        
        var fs = new MockFileSystem();
        fs.Initialize()
            .WithFile(tempFile)
            .Which(f => f.HasBytesContent(new byte[100]));

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Playing,
            fs.FileInfo.New(tempFile),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        using var audioStream = streamOrError.Value;

        var result = audioStream.Seek(TimeSpan.FromDays(1), AudioStream.SeekMode.Position);
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(audioStream.Position).IsEqualTo(audioStream.Length);
    }

    [Test]
    public async Task PauseAndResume_ChangeState()
    {
        const string tempFile = "tempAudioFile";
        
        var fs = new MockFileSystem();
        fs.Initialize()
            .WithFile(tempFile)
            .Which(f => f.HasBytesContent(new byte[100]));

        await using var output = new MemoryStream();
        var streamOrError = AudioStream.Load(
            AudioStream.AudioState.Playing,
            fs.FileInfo.New(tempFile),
            output,
            fs,
            NullLogger.Instance,
            CancellationToken.None
        );

        await Assert.That(streamOrError.IsError).IsFalse();
        using var audioStream = streamOrError.Value;

        audioStream.Pause();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Silence);

        audioStream.Resume();
        await Assert.That(audioStream.State).IsEqualTo(AudioStream.AudioState.Playing);
    }
}
