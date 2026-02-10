using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.YouTube;
using Flurl;

namespace DiscordMusic.Core.Tests.YouTube;

public class YouTubeTrackMapperTests
{
    [Test]
    public async Task ToTrack_MapsTitleToName_AndChannelToArtists()
    {
        var yt = new YouTubeTrack(
            Title: "My Song",
            Channel: "My Channel",
            Url: new Url("https://example.com/watch?v=123"),
            Duration: 42
        );

        var track = YouTubeTrackMapper.ToTrack(yt);

        await Assert.That(track.Name).IsEqualTo("My Song");
        await Assert.That(track.Artists).IsEqualTo("My Channel");
        await Assert.That(track.Url.ToString()).Contains("example.com");
        await Assert.That(track.Duration).IsEqualTo(TimeSpan.FromSeconds(42));
    }
}
