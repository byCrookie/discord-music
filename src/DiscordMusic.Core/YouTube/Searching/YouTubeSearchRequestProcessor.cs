using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Tracks;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Downloading;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Searching;

internal sealed class YouTubeSearchRequestProcessor(
    ILogger<YouTubeSearchRequestProcessor> logger,
    IDiscordFeedbackService feedback,
    ISpotifySearch spotifySearch,
    IYouTubeSearch youtubeSearch,
    ITrackQueue trackQueue,
    ITrackStorage trackStorage,
    IYouTubeDownloadScheduler downloadScheduler
) : IYouTubeSearchRequestProcessor
{
    public async Task ProcessAsync(
        YouTubeSearchRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Processing play request. GuildId={GuildId}, UserId={UserId}, Placement={Placement}, Query={Query}",
            request.Origin.GuildId,
            request.Origin.UserId,
            request.Placement,
            request.Query
        );

        var tracks = spotifySearch.IsSpotifyQuery(request.Query)
            ? await SearchYouTubeFromSpotifyAsync(request, cancellationToken)
            : await SearchYouTubeAsync(request.Query, request.Origin, cancellationToken);
        if (tracks.Count == 0)
        {
            await feedback.SendPrivateAsync(
                request.Origin,
                $"No tracks found for `{request.Query}`.",
                cancellationToken
            );
            return;
        }

        var tracksToEnqueue =
            request.Placement == TrackQueuePlacement.Next
                ? tracks.AsEnumerable().Reverse()
                : tracks;

        foreach (var track in tracksToEnqueue)
        {
            trackStorage.SaveMetadata(track);
            var queuedTrack = new QueuedTrack(track, QueuedTrackStatus.Pending, request.Origin);
            if (request.Placement == TrackQueuePlacement.Next)
            {
                trackQueue.EnqueueFirst(request.Origin.GuildId, queuedTrack);
            }
            else
            {
                trackQueue.EnqueueLast(request.Origin.GuildId, queuedTrack);
            }
        }

        logger.LogInformation(
            "Queued {TrackCount} track(s). GuildId={GuildId}, Placement={Placement}",
            tracks.Count,
            request.Origin.GuildId,
            request.Placement
        );

        await downloadScheduler.EnsureNextTrackQueuedAsync(
            request.Origin.GuildId,
            cancellationToken
        );

        await feedback.SendPrivateAsync(
            request.Origin,
            tracks.Count == 1
                ? $"Queued **{DiscordResponses.FormatTrack(tracks[0])}**."
                : $"Queued {tracks.Count} tracks for `{request.Query}`.",
            cancellationToken
        );
    }

    private async Task<List<Track>> SearchYouTubeFromSpotifyAsync(
        YouTubeSearchRequest request,
        CancellationToken cancellationToken
    )
    {
        var spotify = await spotifySearch.SearchAsync(request.Query, cancellationToken);
        if (!spotify.IsSuccess)
        {
            logger.LogWarning(
                "Spotify lookup failed. GuildId={GuildId}, Query={Query}, Error={Error}",
                request.Origin.GuildId,
                request.Query,
                spotify.ToErrorContent()
            );
            await feedback.SendPrivateAsync(
                request.Origin,
                $"Spotify lookup failed for `{request.Query}`: {spotify.ToErrorContent()}",
                cancellationToken
            );
            return [];
        }

        if (spotify.Value.Count == 0)
        {
            return [];
        }

        var tracks = new List<Track>();
        foreach (var spotifyTrack in spotify.Value)
        {
            var query = BuildYouTubeQuery(spotifyTrack);
            var youtubeTracks = await SearchYouTubeAsync(query, request.Origin, cancellationToken);
            if (youtubeTracks.Count == 0)
            {
                logger.LogInformation(
                    "No YouTube match for Spotify track. GuildId={GuildId}, Query={Query}",
                    request.Origin.GuildId,
                    query
                );
                await feedback.SendPrivateAsync(
                    request.Origin,
                    $"No YouTube match found for Spotify track `{query}`.",
                    cancellationToken
                );
                continue;
            }

            tracks.Add(youtubeTracks[0]);
        }

        return tracks;
    }

    private async Task<List<Track>> SearchYouTubeAsync(
        string query,
        DiscordRequestOrigin origin,
        CancellationToken cancellationToken
    )
    {
        var search = await youtubeSearch.SearchAsync(query, cancellationToken);

        if (!search.IsSuccess)
        {
            logger.LogWarning(
                "YouTube search failed. ChannelId={ChannelId}, Query={Query}, Error={Error}",
                origin.ChannelId,
                query,
                search.ToErrorContent()
            );
            await feedback.SendPrivateAsync(
                origin,
                $"YouTube search failed for `{query}`: {search.ToErrorContent()}",
                cancellationToken
            );
            return [];
        }

        return search.Value.Select(ytTrack => ytTrack.ToTrack()).ToList();
    }

    private static string BuildYouTubeQuery(SpotifyTrack track)
    {
        return $"{track.Name} {track.Artists}".Trim();
    }
}
