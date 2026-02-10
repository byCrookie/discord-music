using System.Diagnostics;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.Sessions;

internal class GuildSession(
    ILogger<GuildSession> logger,
    IYoutubeSearch youtubeSearch,
    IYouTubeDownload youTubeDownload,
    IMusicCache musicCache,
    ISpotifySearch spotifySearch,
    ILogger<Queue.Queue<Track>> queueLogger,
    Guild guild,
    TextChannel textChannel,
    GuildVoiceSession guildVoiceSession,
    GatewayClient gatewayClient
)
{
    private readonly AsyncLock _commandLock = new();
    private readonly Queue.Queue<Track> _queue = new(queueLogger);
    private Track? _currentTrack;

    public Guild Guild { get; } = guild;
    public GuildVoiceSession GuildVoiceSession { get; private set; } = guildVoiceSession;

    public async Task UpdateGuildVoiceSessionAsync(
        GuildVoiceSession newSession,
        CancellationToken ct
    )
    {
        await using var _ = await _commandLock.AquireAsync(ct);
        await GuildVoiceSession.DisposeAsync();
        GuildVoiceSession = newSession;
    }

    public async Task<ErrorOr<AudioUpdate>> PlayAsync(string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        await using var _ = await _commandLock.AquireAsync(ct);
        return await PlayFromQueryAsync(query, true, ct);
    }

    public async Task<ErrorOr<AudioUpdate>> PlayNextAsync(string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        await using var _ = await _commandLock.AquireAsync(ct);
        return await PlayFromQueryAsync(query, false, ct);
    }

    public async Task<ErrorOr<Success>> QueueClearAsync(CancellationToken ct)
    {
        logger.LogTrace("Queue");
        await using var _ = await _commandLock.AquireAsync(ct);
        _queue.Clear();
        return Result.Success;
    }

    public async Task<ErrorOr<AudioUpdate>> SeekAsync(
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    )
    {
        logger.LogTrace("Seek {Mode}", mode);
        await using var _ = await _commandLock.AquireAsync(ct);

        var seek = await GuildVoiceSession.AudioPlayer.SeekAsync(time, mode, ct);

        if (seek.IsError)
        {
            return seek.Errors;
        }

        return await BuildAudioUpdateAsync();
    }

    public async Task<ErrorOr<AudioUpdate>> ShuffleAsync(CancellationToken ct)
    {
        logger.LogTrace("Shuffle");
        await using var _ = await _commandLock.AquireAsync(ct);

        _queue.Shuffle();
        DownloadNextTrackAsync(ct).FireAndForget(logger);

        return await BuildAudioUpdateAsync();
    }

    public async Task<ErrorOr<AudioUpdate>> SkipAsync(int toIndex, CancellationToken ct)
    {
        logger.LogTrace("Skip");
        await using var _ = await _commandLock.AquireAsync(ct);
        _queue.SkipTo(toIndex);
        return await PlayNextTrackFromQueueAsync(true, ct);
    }

    public async Task<ErrorOr<IReadOnlyList<Track>>> QueueAsync(CancellationToken ct)
    {
        logger.LogTrace("Queue");
        await using var _ = await _commandLock.AquireAsync(ct);
        return ErrorOrFactory.From(_queue.Items());
    }

    public async Task<ErrorOr<AudioUpdate>> PauseAsync(CancellationToken ct)
    {
        logger.LogTrace("Pause");
        await using var _ = await _commandLock.AquireAsync(ct);

        var pause = await GuildVoiceSession.AudioPlayer.PauseAsync(ct);

        if (pause.IsError)
        {
            return pause.Errors;
        }

        return await BuildAudioUpdateAsync();
    }

    public async Task<ErrorOr<AudioUpdate>> ResumeAsync(CancellationToken ct)
    {
        logger.LogTrace("Resume");
        await using var _ = await _commandLock.AquireAsync(ct);

        var resume = await GuildVoiceSession.AudioPlayer.ResumeAsync(ct);

        if (resume.IsError)
        {
            return resume.Errors;
        }

        return await BuildAudioUpdateAsync();
    }

    public async Task<ErrorOr<AudioUpdate>> NowPlayingAsync(CancellationToken ct)
    {
        logger.LogTrace("Now Playing");
        await using var _ = await _commandLock.AquireAsync(ct);
        return await BuildAudioUpdateAsync();
    }

    private async Task<ErrorOr<AudioUpdate>> PlayFromQueryAsync(
        string query,
        bool append,
        CancellationToken ct
    )
    {
        var baseMetadata = new Dictionary<string, object?>
        {
            [ErrorExtensions.MetadataKeys.Operation] = append ? "play" : "playNext",
            ["guild.id"] = Guild.Id,
            ["textChannel.id"] = textChannel.Id,
            ["query"] = query,
        };

        if (spotifySearch.IsSpotifyQuery(query))
        {
            var searchSpotify = await spotifySearch.SearchAsync(query, ct);

            if (searchSpotify.IsError)
            {
                logger.LogWarning(
                    "Spotify search failed. GuildId={GuildId} TextChannelId={TextChannelId} Query={Query} ErrorCode={ErrorCode}",
                    Guild.Id,
                    textChannel.Id,
                    query,
                    searchSpotify.FirstError.Code
                );

                return searchSpotify.WithMetadata(baseMetadata).Errors;
            }

            if (searchSpotify.Value.Count == 0)
            {
                return Error
                    .NotFound(description: "No tracks found")
                    .WithMetadata(baseMetadata)
                    .WithMetadata("source", "spotify");
            }

            var spotifyTracks = searchSpotify
                .Value.Select(track => new Track(
                    track.Name,
                    track.Artists,
                    track.Url,
                    TimeSpan.Zero
                ))
                .ToList();

            foreach (var track in spotifyTracks)
            {
                if (append)
                {
                    _queue.EnqueueLast(track);
                }
                else
                {
                    _queue.EnqueueFirst(track);
                }
            }

            return await PlayNextTrackFromQueueAsync(false, ct);
        }

        var search = await youtubeSearch.SearchAsync(query, ct);
        if (search.IsError)
        {
            logger.LogWarning(
                "YouTube search failed. GuildId={GuildId} TextChannelId={TextChannelId} Query={Query} ErrorCode={ErrorCode}",
                Guild.Id,
                textChannel.Id,
                query,
                search.FirstError.Code
            );

            return search.WithMetadata(baseMetadata).Errors;
        }

        if (search.Value.Count == 0)
        {
            return Error
                .NotFound(description: "No tracks found")
                .WithMetadata(baseMetadata)
                .WithMetadata("source", "youtube");
        }

        var tracks = search
            .Value.Select(track => new Track(
                track.Title,
                track.Channel,
                track.Url,
                TimeSpan.FromSeconds(track.Duration ?? 0)
            ))
            .ToList();

        foreach (var track in tracks)
        {
            if (append)
            {
                _queue.EnqueueLast(track);
            }
            else
            {
                _queue.EnqueueFirst(track);
            }
        }

        return await PlayNextTrackFromQueueAsync(false, ct);
    }

    private async Task UpdateAsync(AudioEvent item, Exception? exception, CancellationToken ct)
    {
        logger.LogTrace(
            "Audio event received. Event={AudioEvent} GuildId={GuildId} TextChannelId={TextChannelId} CurrentTrack={CurrentTrackUrl}",
            item,
            Guild.Id,
            textChannel.Id,
            _currentTrack?.Url
        );

        switch (item)
        {
            case AudioEvent.Error:
            {
                logger.LogError(
                    exception,
                    "Error in audio stream. GuildId={GuildId} TextChannelId={TextChannelId} CurrentTrack={CurrentTrackUrl}",
                    Guild.Id,
                    textChannel.Id,
                    _currentTrack?.Url
                );

                var error = Error
                    .Unexpected(
                        code: "Audio.StreamError",
                        description: "Playback failed. I'll try the next track."
                    )
                    .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "audio.update")
                    .WithMetadata("guild.id", Guild.Id)
                    .WithMetadata("textChannel.id", textChannel.Id)
                    .WithMetadata("track.url", _currentTrack?.Url);

                if (exception is not null)
                {
                    error = error.WithException(exception);
                }

                await textChannel.SendMessageAsync(
                    new MessageProperties { Content = ((ErrorOr<Success>)error).ToErrorContent() },
                    cancellationToken: ct
                );

                // We're in a terminal state for the current track after an error.
                _currentTrack = null;

                // Just like on Ended: keep skipping forward until we successfully start something
                // or the queue is empty. This avoids getting "stuck" requiring a manual skip.
                while (true)
                {
                    var nextFromError = await PlayNextTrackFromQueueAsync(true, ct);

                    if (nextFromError.IsError)
                    {
                        logger.LogError(
                            "Failed to play next track after stream error. GuildId={GuildId} TextChannelId={TextChannelId} ErrorCode={ErrorCode}",
                            Guild.Id,
                            textChannel.Id,
                            nextFromError.FirstError.Code
                        );

                        await textChannel.SendMessageAsync(
                            new MessageProperties { Content = nextFromError.ToErrorContent() },
                            cancellationToken: ct
                        );

                        if (_queue.Count() > 0)
                        {
                            continue;
                        }

                        return;
                    }

                    if (nextFromError.Value.Track is null)
                    {
                        logger.LogTrace("No more tracks in queue. GuildId={GuildId}", Guild.Id);
                        await textChannel.SendMessageAsync(
                            new MessageProperties
                            {
                                Content = "Queue is empty. No more tracks to play.",
                            },
                            cancellationToken: ct
                        );
                    }

                    return;
                }
            }
            case AudioEvent.Ended:
            {
                logger.LogTrace(
                    "Track ended. GuildId={GuildId} TextChannelId={TextChannelId} LastTrack={CurrentTrackUrl}",
                    Guild.Id,
                    textChannel.Id,
                    _currentTrack?.Url
                );

                // Reached a terminal state for the current track.
                // Clear it before attempting to start the next one so PlayNextTrackFromQueueAsync
                // doesn't treat us as "still playing" when something goes wrong.
                _currentTrack = null;

                while (true)
                {
                    var next = await PlayNextTrackFromQueueAsync(true, ct);

                    if (next.IsError)
                    {
                        logger.LogError(
                            "Failed to play next track. GuildId={GuildId} TextChannelId={TextChannelId} ErrorCode={ErrorCode}",
                            Guild.Id,
                            textChannel.Id,
                            next.FirstError.Code
                        );

                        await textChannel.SendMessageAsync(
                            new MessageProperties { Content = next.ToErrorContent() },
                            cancellationToken: ct
                        );

                        if (_queue.Count() > 0)
                        {
                            continue;
                        }

                        return;
                    }

                    if (next.Value.Track is null)
                    {
                        logger.LogTrace("No more tracks in queue. GuildId={GuildId}", Guild.Id);
                        await textChannel.SendMessageAsync(
                            new MessageProperties
                            {
                                Content = "Queue is empty. No more tracks to play.",
                            },
                            cancellationToken: ct
                        );
                    }

                    break;
                }

                break;
            }
            case AudioEvent.None:
                break;
            default:
                throw new UnreachableException($"Unknown audio event: {item}");
        }
    }

    private async Task<ErrorOr<AudioUpdate>> PlayNextTrackFromQueueAsync(
        bool now,
        CancellationToken ct
    )
    {
        var status = await GuildVoiceSession.AudioPlayer.StatusAsync(ct);

        if (_currentTrack is not null && !now && status.State != AudioState.Ended)
        {
            DownloadNextTrackAsync(ct).FireAndForget(logger);
            return await BuildAudioUpdateAsync();
        }

        if (!_queue.TryDequeue(out var firstTrack))
        {
            logger.LogTrace(
                "Queue empty on PlayNext. GuildId={GuildId} TextChannelId={TextChannelId}",
                Guild.Id,
                textChannel.Id
            );
            return await BuildAudioUpdateAsync();
        }

        logger.LogInformation(
            "Starting playback. GuildId={GuildId} TextChannelId={TextChannelId} TrackUrl={TrackUrl} TrackName={TrackName} TrackArtists={TrackArtists}",
            Guild.Id,
            textChannel.Id,
            firstTrack.Url,
            firstTrack.Name,
            firstTrack.Artists
        );

        if (spotifySearch.IsSpotifyQuery(firstTrack.Url))
        {
            logger.LogDebug(
                "Spotify track detected, searching on YouTube. GuildId={GuildId} TrackName={TrackName} TrackArtists={TrackArtists}",
                Guild.Id,
                firstTrack.Name,
                firstTrack.Artists
            );

            var search = await youtubeSearch.SearchAsync(
                $"{firstTrack.Name} {firstTrack.Artists}",
                ct
            );

            if (search.IsError)
            {
                return search
                    .WithMetadata(
                        ErrorExtensions.MetadataKeys.Operation,
                        "queue.next.spotify.youtubeSearch"
                    )
                    .WithMetadata("guild.id", Guild.Id)
                    .WithMetadata("textChannel.id", textChannel.Id)
                    .WithMetadata("track.url", firstTrack.Url)
                    .WithMetadata("track.name", firstTrack.Name)
                    .WithMetadata("track.artists", firstTrack.Artists)
                    .Errors;
            }

            if (search.Value.Count == 0)
            {
                return Error
                    .NotFound(description: "Did not find next track")
                    .WithMetadata(
                        ErrorExtensions.MetadataKeys.Operation,
                        "queue.next.spotify.youtubeSearch"
                    )
                    .WithMetadata("guild.id", Guild.Id)
                    .WithMetadata("textChannel.id", textChannel.Id)
                    .WithMetadata("track.url", firstTrack.Url)
                    .WithMetadata("track.name", firstTrack.Name)
                    .WithMetadata("track.artists", firstTrack.Artists);
            }

            var track = new Track(
                search.Value.First().Channel,
                search.Value.First().Title,
                search.Value.First().Url,
                TimeSpan.FromSeconds(search.Value.First().Duration ?? 0)
            );

            var update = await musicCache.AddOrUpdateTrackAsync(
                firstTrack,
                track,
                Pcm16Bytes.ToBytes(track.Duration * (5d / 4d)).Humanize(),
                ct
            );

            if (update.IsError)
            {
                return update
                    .WithMetadata(
                        ErrorExtensions.MetadataKeys.Operation,
                        "queue.next.spotify.cache.update"
                    )
                    .WithMetadata("guild.id", Guild.Id)
                    .WithMetadata("textChannel.id", textChannel.Id)
                    .WithMetadata("track.url", track.Url)
                    .WithMetadata("track.name", track.Name)
                    .WithMetadata("track.artists", track.Artists)
                    .Errors;
            }

            firstTrack = track;
        }

        var cache = await musicCache.GetOrAddTrackAsync(
            firstTrack,
            Pcm16Bytes.ToBytes(firstTrack.Duration * (5d / 4d)).Humanize(),
            ct
        );

        if (cache.IsError)
        {
            return cache
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.next.cache.getOrAdd")
                .WithMetadata("guild.id", Guild.Id)
                .WithMetadata("textChannel.id", textChannel.Id)
                .WithMetadata("track.url", firstTrack.Url)
                .WithMetadata("track.name", firstTrack.Name)
                .WithMetadata("track.artists", firstTrack.Artists)
                .Errors;
        }

        if (cache.Value.Exists())
        {
            logger.LogDebug(
                "Playing cached track. GuildId={GuildId} TrackUrl={TrackUrl}",
                Guild.Id,
                firstTrack.Url
            );

            var playExisting = await GuildVoiceSession.AudioPlayer.PlayAsync(
                cache.Value,
                UpdateAsync,
                ct
            );

            if (playExisting.IsError)
            {
                return playExisting
                    .WithMetadata(
                        ErrorExtensions.MetadataKeys.Operation,
                        "queue.next.player.play.cached"
                    )
                    .WithMetadata("guild.id", Guild.Id)
                    .WithMetadata("textChannel.id", textChannel.Id)
                    .WithMetadata("track.url", firstTrack.Url)
                    .WithMetadata("track.name", firstTrack.Name)
                    .WithMetadata("track.artists", firstTrack.Artists)
                    .Errors;
            }

            _currentTrack = firstTrack;
            DownloadNextTrackAsync(ct).FireAndForget(logger);
            return await BuildAudioUpdateAsync();
        }

        var download = await youTubeDownload.DownloadAsync(
            $"{firstTrack.Name} {firstTrack.Artists}",
            cache.Value,
            ct
        );

        if (download.IsError)
        {
            return download
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.next.youtube.download")
                .WithMetadata("guild.id", Guild.Id)
                .WithMetadata("textChannel.id", textChannel.Id)
                .WithMetadata("track.url", firstTrack.Url)
                .WithMetadata("track.name", firstTrack.Name)
                .WithMetadata("track.artists", firstTrack.Artists)
                .Errors;
        }

        var play = await GuildVoiceSession.AudioPlayer.PlayAsync(cache.Value, UpdateAsync, ct);

        if (play.IsError)
        {
            return play.WithMetadata(
                    ErrorExtensions.MetadataKeys.Operation,
                    "queue.next.player.play.downloaded"
                )
                .WithMetadata("guild.id", Guild.Id)
                .WithMetadata("textChannel.id", textChannel.Id)
                .WithMetadata("track.url", firstTrack.Url)
                .WithMetadata("track.name", firstTrack.Name)
                .WithMetadata("track.artists", firstTrack.Artists)
                .Errors;
        }

        _currentTrack = firstTrack;
        DownloadNextTrackAsync(ct).FireAndForget(logger);
        return await BuildAudioUpdateAsync();
    }

    private async Task<AudioUpdate> BuildAudioUpdateAsync()
    {
        var status = await GuildVoiceSession.AudioPlayer.StatusAsync(CancellationToken.None);
        var nextTrack = _queue.TryPeek(out var next) ? next : null;
        return new AudioUpdate(_currentTrack, nextTrack, status);
    }

    private async Task DownloadNextTrackAsync(CancellationToken ct)
    {
        if (_queue.TryPeek(out var nextTrack))
        {
            logger.LogDebug(
                "Pre-downloading next track. GuildId={GuildId} TextChannelId={TextChannelId} TrackUrl={TrackUrl}",
                Guild.Id,
                textChannel.Id,
                nextTrack.Url
            );

            if (spotifySearch.IsSpotifyQuery(nextTrack.Url))
            {
                var search = await youtubeSearch.SearchAsync(
                    $"{nextTrack.Name} {nextTrack.Artists}",
                    ct
                );

                if (search.IsError)
                {
                    logger.LogError(
                        "Failed to pre-download next track (YouTube search). GuildId={GuildId} TrackUrl={TrackUrl} ErrorCode={ErrorCode}",
                        Guild.Id,
                        nextTrack.Url,
                        search.FirstError.Code
                    );

                    _queue.TryDequeue(out _);
                    var audioUpdate = await BuildAudioUpdateAsync();
                    await gatewayClient.Rest.SendMessageAsync(
                        textChannel.Id,
                        new MessageProperties
                        {
                            Content = $"""
                                       {search
                                           .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.predownload.spotify.youtubeSearch")
                                           .WithMetadata("guild.id", Guild.Id)
                                           .WithMetadata("textChannel.id", textChannel.Id)
                                           .WithMetadata("track.url", nextTrack.Url)
                                           .ToErrorContent()}
                                       {audioUpdate.ToValueContent()}
                                       """,
                        },
                        cancellationToken: ct
                    );
                    return;
                }

                if (search.Value.Count == 0)
                {
                    logger.LogError(
                        "Did not find next track during pre-download. GuildId={GuildId} TrackName={TrackName} TrackArtists={TrackArtists}",
                        Guild.Id,
                        nextTrack.Name,
                        nextTrack.Artists
                    );
                    return;
                }

                var track = new Track(
                    search.Value.First().Channel,
                    search.Value.First().Title,
                    search.Value.First().Url,
                    TimeSpan.FromSeconds(search.Value.First().Duration ?? 0)
                );

                var update = await musicCache.AddOrUpdateTrackAsync(
                    nextTrack,
                    track,
                    Pcm16Bytes.ToBytes(track.Duration * (5d / 4d)).Humanize(),
                    ct
                );

                if (update.IsError)
                {
                    logger.LogError(
                        "Failed to update next track during pre-download. GuildId={GuildId} ErrorCode={ErrorCode}",
                        Guild.Id,
                        update.FirstError.Code
                    );
                    _queue.TryDequeue(out _);
                    var audioUpdate = await BuildAudioUpdateAsync();
                    await gatewayClient.Rest.SendMessageAsync(
                        textChannel.Id,
                        new MessageProperties
                        {
                            Content = $"""
                                       {update
                                           .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.predownload.spotify.cache.update")
                                           .WithMetadata("guild.id", Guild.Id)
                                           .WithMetadata("textChannel.id", textChannel.Id)
                                           .WithMetadata("track.url", track.Url)
                                           .ToErrorContent()}
                                       {audioUpdate.ToValueContent()}
                                       """,
                        },
                        cancellationToken: ct
                    );
                    return;
                }

                nextTrack = track;
            }

            var nextCache = await musicCache.GetOrAddTrackAsync(
                nextTrack,
                Pcm16Bytes.ToBytes(nextTrack.Duration * (5d / 4d)).Humanize(),
                ct
            );

            if (nextCache.IsError)
            {
                logger.LogError(
                    "Failed to get or add next track to cache during pre-download. GuildId={GuildId} TrackUrl={TrackUrl} ErrorCode={ErrorCode}",
                    Guild.Id,
                    nextTrack.Url,
                    nextCache.FirstError.Code
                );
                _queue.TryDequeue(out _);
                var audioUpdate = await BuildAudioUpdateAsync();
                await gatewayClient.Rest.SendMessageAsync(
                    textChannel.Id,
                    new MessageProperties
                    {
                        Content = $"""
                                   {nextCache
                                       .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.predownload.cache.getOrAdd")
                                       .WithMetadata("guild.id", Guild.Id)
                                       .WithMetadata("textChannel.id", textChannel.Id)
                                       .WithMetadata("track.url", nextTrack.Url)
                                       .ToErrorContent()}
                                   {audioUpdate.ToValueContent()}
                                   """,
                    },
                    cancellationToken: ct
                );

                return;
            }

            if (!nextCache.Value.Exists())
            {
                var download = await youTubeDownload.DownloadAsync(
                    $"{nextTrack.Name} {nextTrack.Artists}",
                    nextCache.Value,
                    ct
                );

                if (download.IsError)
                {
                    logger.LogError(
                        "Failed to download next track during pre-download. GuildId={GuildId} TrackUrl={TrackUrl} ErrorCode={ErrorCode}",
                        Guild.Id,
                        nextTrack.Url,
                        download.FirstError.Code
                    );
                    _queue.TryDequeue(out _);
                    var audioUpdate = await BuildAudioUpdateAsync();
                    await gatewayClient.Rest.SendMessageAsync(
                        textChannel.Id,
                        new MessageProperties
                        {
                            Content = $"""
                                       {download
                                           .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "queue.predownload.youtube.download")
                                           .WithMetadata("guild.id", Guild.Id)
                                           .WithMetadata("textChannel.id", textChannel.Id)
                                           .WithMetadata("track.url", nextTrack.Url)
                                           .ToErrorContent()}
                                       {audioUpdate.ToValueContent()}
                                       """,
                        },
                        cancellationToken: ct
                    );
                    return;
                }
            }

            logger.LogDebug(
                "Pre-download completed. GuildId={GuildId} TrackUrl={TrackUrl}",
                Guild.Id,
                nextTrack.Url
            );
        }
    }
}
