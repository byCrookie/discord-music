using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using Flurl;

namespace DiscordMusic.Core.Tests.Discord;

public class AudioBarRendererTests
{
    private static readonly Track Track = new(
        "track-id",
        "Hello",
        "Adele",
        new Url("https://example.com/track"),
        TimeSpan.FromMinutes(6) + TimeSpan.FromSeconds(7)
    );

    [Test]
    public async Task StandaloneRenderShowsDurationOnlyInProgress()
    {
        var rendered = AudioBarRenderer.Render(
            new PlaybackSession.PlaybackSnapshot(
                PlaybackState.Playing,
                Track,
                TimeSpan.FromSeconds(49)
            )
        );

        await Assert.That(rendered).Contains("### Playing");
        await Assert.That(rendered).Contains("Hello - Adele");
        await Assert.That(rendered).DoesNotContain("Hello - Adele [6 minutes 7 seconds]");
        await Assert.That(rendered).Contains("49 seconds / 6 minutes 7 seconds");
    }

    [Test]
    public async Task InlineRenderAvoidsSecondHeading()
    {
        var rendered = AudioBarRenderer.Render(
            new PlaybackSession.PlaybackSnapshot(
                PlaybackState.Playing,
                Track,
                TimeSpan.FromSeconds(15)
            ),
            mode: AudioBarRenderMode.Inline
        );

        await Assert.That(rendered).Contains("**Now playing:** Hello - Adele");
        await Assert.That(rendered).DoesNotContain("### Playing");
        await Assert.That(rendered).DoesNotContain("Hello - Adele [6 minutes 7 seconds]");
        await Assert.That(rendered).Contains("15 seconds / 6 minutes 7 seconds");
    }
}
