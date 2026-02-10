using DiscordMusic.Core.Discord;

namespace DiscordMusic.Core.Tests.Discord;

public class VoiceStateChangeClassifierTests
{
    [Test]
    public async Task Classify_WhenUserJoins_ReturnsJoined()
    {
        var change = VoiceStateChangeClassifier.Classify(
            userId: 123,
            botId: 999,
            previousChannelId: null,
            currentChannelId: 456
        );

        await Assert.That(change).IsEqualTo(VoiceStateChange.Joined);
    }

    [Test]
    public async Task Classify_WhenUserLeaves_ReturnsLeft()
    {
        var change = VoiceStateChangeClassifier.Classify(
            userId: 123,
            botId: 999,
            previousChannelId: 456,
            currentChannelId: null
        );

        await Assert.That(change).IsEqualTo(VoiceStateChange.Left);
    }

    [Test]
    public async Task Classify_WhenUserMoves_ReturnsMoved()
    {
        var change = VoiceStateChangeClassifier.Classify(
            userId: 123,
            botId: 999,
            previousChannelId: 456,
            currentChannelId: 789
        );

        await Assert.That(change).IsEqualTo(VoiceStateChange.Moved);
    }

    [Test]
    public async Task Classify_WhenNoChange_ReturnsUnknown()
    {
        var change = VoiceStateChangeClassifier.Classify(
            userId: 123,
            botId: 999,
            previousChannelId: 456,
            currentChannelId: 456
        );

        await Assert.That(change).IsEqualTo(VoiceStateChange.Unknown);
    }
}
