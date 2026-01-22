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
    GuildVoiceSession guildVoiceSession)
{
    private readonly AsyncLock _commandLock = new();
    private readonly Queue.Queue<Track> _queue = new(queueLogger);
    private Track? _currentTrack;

    public Guild Guild { get; } = guild;
    public GuildVoiceSession GuildVoiceSession { get; private set; } = guildVoiceSession;

    public async Task UpdateGuildVoiceSessionAsync(GuildVoiceSession newSession,
        CancellationToken ct)
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
        if (spotifySearch.IsSpotifyQuery(query))
        {
            var searchSpotify = await spotifySearch.SearchAsync(query, ct);

            if (searchSpotify.IsError)
            {
                return searchSpotify.Errors;
            }

            if (searchSpotify.Value.Count == 0)
            {
                return Error.NotFound(description: "No tracks found");
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
            return search.Errors;
        }

        if (search.Value.Count == 0)
        {
            return Error.NotFound(description: "No tracks found");
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

    private async Task UpdateAsync(
        AudioEvent item,
        Exception? exception,
        CancellationToken ct
    )
    {
        logger.LogTrace("Update {Item}", item);

        switch (item)
        {
            case AudioEvent.Error:
                logger.LogError(exception, "Error in audio stream");
                await textChannel.SendMessageAsync(
                    new MessageProperties
                    {
                        Content = ExceptionToContent(
                            "Error in audio stream. Trying to play the next track.",
                            exception
                        ),
                    },
                    cancellationToken: ct
                );
                _currentTrack = null;

                var nextFromError = await PlayNextTrackFromQueueAsync(true, ct);

                if (nextFromError.IsError)
                {
                    logger.LogError(
                        "Failed to play next track: {Error}",
                        nextFromError.ToErrorContent()
                    );
                    await textChannel.SendMessageAsync(
                        new MessageProperties { Content = nextFromError.ToErrorContent() },
                        cancellationToken: ct
                    );
                    return;
                }

                if (nextFromError.Value.Track is null)
                {
                    logger.LogTrace("No more tracks in queue");
                    await textChannel.SendMessageAsync(
                        new MessageProperties
                            { Content = "Queue empty. No more tracks in _queue." },
                        cancellationToken: ct
                    );
                }

                return;
            case AudioEvent.Ended:
            {
                logger.LogTrace("Track ended");
                var next = await PlayNextTrackFromQueueAsync(true, ct);

                if (next.IsError)
                {
                    logger.LogError("Failed to play next track: {Error}", next.ToErrorContent());
                    await textChannel.SendMessageAsync(
                        new MessageProperties { Content = next.ToErrorContent() },
                        cancellationToken: ct
                    );
                    return;
                }

                if (next.Value.Track is null)
                {
                    logger.LogTrace("No more tracks in queue");
                    await textChannel.SendMessageAsync(
                        new MessageProperties
                            { Content = "Queue empty. No more tracks in _queue." },
                        cancellationToken: ct
                    );
                }

                break;
            }
            case AudioEvent.None:
                break;
            default:
                throw new UnreachableException($"Unknown audio event: {item}");
        }

        return;

        string ExceptionToContent(string message, Exception? ex)
        {
            return ex is null
                ? message
                : $"""
                   ### **ERROR**: {message}
                   ```{ex}```
                   """;
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
            return await BuildAudioUpdateAsync();
        }

        if (spotifySearch.IsSpotifyQuery(firstTrack.Url))
        {
            logger.LogDebug(
                "Use YouTube to search for Spotify track {Name} {Artists}",
                firstTrack.Name,
                firstTrack.Artists
            );

            var search = await youtubeSearch.SearchAsync(
                $"{firstTrack.Name} {firstTrack.Artists}",
                ct
            );

            if (search.IsError)
            {
                return search.Errors;
            }

            if (search.Value.Count == 0)
            {
                return Error.NotFound(description: "Did not find next track");
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
                return update.Errors;
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
            return cache.Errors;
        }

        if (cache.Value.Exists())
        {
            logger.LogDebug(
                "Playing existing track {Name} {Artists}",
                firstTrack.Name,
                firstTrack.Artists
            );

            var playExisting =
                await GuildVoiceSession.AudioPlayer.PlayAsync(cache.Value, UpdateAsync, ct);

            if (playExisting.IsError)
            {
                return playExisting.Errors;
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
            return download.Errors;
        }

        var play = await GuildVoiceSession.AudioPlayer.PlayAsync(cache.Value, UpdateAsync, ct);

        if (play.IsError)
        {
            return play.Errors;
        }

        _currentTrack = firstTrack;
        DownloadNextTrackAsync(ct).FireAndForget(logger);
        return await BuildAudioUpdateAsync();
    }

    private async Task<AudioUpdate> BuildAudioUpdateAsync()
    {
        var status = await GuildVoiceSession.AudioPlayer.StatusAsync(CancellationToken.None);
        var nextTrack = _queue.TryPeek(out var next) ? next : null;
        return new AudioUpdate(
            _currentTrack,
            nextTrack,
            status
        );
    }

    private async Task DownloadNextTrackAsync
        (CancellationToken ct)
    {
        if (_queue.TryPeek(out var nextTrack))
        {
            logger.LogDebug(
                "Downloading next track {Name} {Artists} in the background",
                nextTrack.Name,
                nextTrack.Artists
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
                        "Failed to download next track: {Error}",
                        search.ToErrorContent()
                    );
                    return;
                }

                if (search.Value.Count == 0)
                {
                    logger.LogError("Did not find next track");
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
                        "Failed to update next track: {Error}",
                        update.ToErrorContent()
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
                    "Failed to get or add next track to cache: {Error}",
                    nextCache.ToErrorContent()
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
                        "Failed to download next track: {Error}",
                        download.ToErrorContent()
                    );
                    return;
                }
            }

            logger.LogDebug(
                "Downloaded next track {Name} {Artists} in the background",
                nextTrack.Name,
                nextTrack.Artists
            );
        }
    }
}
