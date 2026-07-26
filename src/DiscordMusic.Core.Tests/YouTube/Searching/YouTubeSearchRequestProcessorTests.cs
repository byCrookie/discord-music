using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.YouTube.Downloading;
using DiscordMusic.Core.YouTube.Searching;
using ErrorOr;
using Flurl;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DiscordMusic.Core.Tests.YouTube.Searching;

public class YouTubeSearchRequestProcessorTests
{
    private const ulong GuildId = 1;

    [Test]
    public async Task ProcessAsyncQueuesCachedTracksAsAvailable()
    {
        var feedback = Substitute.For<IDiscordFeedbackService>();
        var spotifySearch = Substitute.For<ISpotifySearch>();
        var youtubeSearch = Substitute.For<IYouTubeSearch>();
        var trackQueue = new TrackQueue(NullLogger<TrackQueue>.Instance);
        var trackStorage = Substitute.For<ITrackStorage>();
        var downloadScheduler = Substitute.For<IYouTubeDownloadScheduler>();
        var youtubeTrack = new YouTubeTrack(
            "Cached Track",
            "Channel",
            180,
            new Url("https://www.youtube.com/watch?v=cached")
        );
        youtubeSearch
            .SearchAsync("cached", Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new List<YouTubeTrack> { youtubeTrack }));
        trackStorage.IsTrackCached(Arg.Any<Track>(), "pcm").Returns(true);
        var processor = new YouTubeSearchRequestProcessor(
            NullLogger<YouTubeSearchRequestProcessor>.Instance,
            feedback,
            spotifySearch,
            youtubeSearch,
            trackQueue,
            trackStorage,
            downloadScheduler,
            new FakeTimeProvider()
        );

        await processor.ProcessAsync(
            new YouTubeSearchRequest("cached", new DiscordRequestOrigin(GuildId, 2, 3)),
            CancellationToken.None
        );

        var queuedTracks = trackQueue.QueuedTracks(GuildId);
        await Assert.That(queuedTracks).Count().IsEqualTo(1);
        await Assert.That(queuedTracks[0].Status).IsEqualTo(QueuedTrackStatus.Available);
        trackStorage.Received(1).SaveMetadata(Arg.Any<Track>());
        await downloadScheduler
            .Received(1)
            .EnsureNextTrackQueuedAsync(GuildId, Arg.Any<CancellationToken>());
    }
}
