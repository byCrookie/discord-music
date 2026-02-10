using DiscordMusic.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Audio;

public class AudioPlayerTests
{
    [Test]
    public async Task PlayAsync_WhenFileDoesNotExist_ReturnsNotFound()
    {
        var fs = new MockFileSystem();
        fs.Initialize();
        await using var output = new MemoryStream();

        var player = new AudioPlayer(
            NullLogger<AudioPlayer>.Instance,
            NullLogger<AudioStream>.Instance,
            fs,
            output
        );

        var file = fs.FileInfo.New("missing.pcm");

        var result = await player.PlayAsync(
            file,
            (_, _, _) => Task.CompletedTask,
            CancellationToken.None
        );

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.FirstError.Type).IsEqualTo(ErrorOr.ErrorType.NotFound);
    }

    [Test]
    public async Task StatusAsync_WhenNothingPlaying_IsStopped()
    {
        var fs = new MockFileSystem();
        fs.Initialize();
        await using var output = new MemoryStream();

        var player = new AudioPlayer(
            NullLogger<AudioPlayer>.Instance,
            NullLogger<AudioStream>.Instance,
            fs,
            output
        );

        var status = await player.StatusAsync(CancellationToken.None);

        await Assert.That(status.State).IsEqualTo(AudioState.Stopped);
        await Assert.That(status.Position).IsEqualTo(TimeSpan.Zero);
        await Assert.That(status.Length).IsEqualTo(TimeSpan.Zero);
    }
}
