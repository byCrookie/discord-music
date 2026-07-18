using DiscordMusic.Core.Audio.Sending;

namespace DiscordMusic.Core.Tests.Discord;

public class TimedAudioSenderTests
{
    [Test]
    public async Task FrameSizeMatchesTwentyMillisecondsOfStereoFloatPcm()
    {
        var oneFrameOffset = TimedAudioSender.CalculateByteOffset(TimedAudioSender.FrameDuration);

        await Assert.That(oneFrameOffset).IsEqualTo(7_680);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 384_000)]
    [Arguments(90, 34_560_000)]
    public async Task CalculateByteOffsetUsesFortyEightKilohertzStereoFloatPcm(
        int seconds,
        long expectedOffset
    )
    {
        var offset = TimedAudioSender.CalculateByteOffset(TimeSpan.FromSeconds(seconds));

        await Assert.That(offset).IsEqualTo(expectedOffset);
    }

    [Test]
    public async Task CalculateByteOffsetAlignsToFrameBoundary()
    {
        var offset = TimedAudioSender.CalculateByteOffset(TimeSpan.FromMilliseconds(25));

        await Assert.That(offset).IsEqualTo(TimedAudioSender.FrameSizeBytes);
    }
}
