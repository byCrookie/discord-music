using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using ErrorOr;
using Humanizer;
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
    private const double CacheSizeSlackFactor = 5d / 4d;

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
        await using var _ = await _commandLock.AcquireAsync(ct);
        await GuildVoiceSession.DisposeAsync();
        GuildVoiceSession = newSession;
    }

    public async Task<ErrorOr<AudioUpdate>> PlayAsync(string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        await using var _ = await _commandLock.AcquireAsync(ct);
        return await PlayFromQueryAsync(query, true, ct);
    }

    public async Task<ErrorOr<AudioUpdate>> PlayNextAsync(string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        await using var _ = await _commandLock.AcquireAsync(ct);
        return await PlayFromQueryAsync(query, false, ct);
    }

    public async Task<ErrorOr<Success>> QueueClearAsync(CancellationToken ct)
    {
        logger.LogTrace("Queue");
        await using var _ = await _commandLock.AcquireAsync(ct);
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
        await using var _ = await _commandLock.AcquireAsync(ct);

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
        await using var _ = await _commandLock.AcquireAsync(ct);

        _queue.Shuffle();
        DownloadNextTrackAsync(ct).FireAndForget(logger);

        return await BuildAudioUpdateAsync();
    }

    public async Task<ErrorOr<AudioUpdate>> SkipAsync(int toIndex, CancellationToken ct)
    {
        logger.LogTrace("Skip");
        await using var _ = await _commandLock.AcquireAsync(ct);
        _queue.SkipTo(toIndex);
        return await PlayNextTrackFromQueueAsync(true, ct);
    }

    public async Task<ErrorOr<IReadOnlyList<Track>>> QueueAsync(CancellationToken ct)
    {
        logger.LogTrace("Queue");
        await using var _ = await _commandLock.AcquireAsync(ct);
        return ErrorOrFactory.From(_queue.Items());
    }

    public async Task<ErrorOr<AudioUpdate>> PauseAsync(CancellationToken ct)
    {
        logger.LogTrace("Pause");
        await using var _ = await _commandLock.AcquireAsync(ct);

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
        await using var _ = await _commandLock.AcquireAsync(ct);

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
        await using var _ = await _commandLock.AcquireAsync(ct);
        return await BuildAudioUpdateAsync();
    }

    public Task ReportIfErrorAsync<T>(ErrorOr<T> result, CancellationToken ct)
    {
        if (!result.IsError)
        {
            return Task.CompletedTask;
        }

        return SendTextAsync(result.ToErrorContent(), ct);
    }

    private void EnqueueTracks(IEnumerable<Track> tracks, bool append)
    {
        if (append)
        {
            foreach (var track in tracks)
            {
                _queue.EnqueueLast(track);
            }

            return;
        }

        // When prepending multiple tracks, enqueue in reverse so the first
        // track in the list becomes the next track to play.
        foreach (var track in tracks.Reverse())
        {
            _queue.EnqueueFirst(track);
        }
    }

    private async Task<ErrorOr<AudioUpdate>> PlayFromQueryAsync(
        string query,
        bool append,
        CancellationToken ct
    )
    {
        var baseMetadata = SessionMetadata(operation: append ? "play" : "playNext", query: query);

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

            EnqueueTracks(spotifyTracks, append);
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

        EnqueueTracks(tracks, append);
        return await PlayNextTrackFromQueueAsync(false, ct);
    }

    private async Task UpdateAsync(AudioEvent item, Exception? exception, CancellationToken ct)
    {
        List<Task> messageTasks;

        await using (await _commandLock.AcquireAsync(ct))
        {
            var currentTrackSnapshot = Volatile.Read(ref _currentTrack);

            logger.LogTrace(
                "Audio event received. Event={AudioEvent} GuildId={GuildId} TextChannelId={TextChannelId} CurrentTrack={CurrentTrackUrl}",
                item,
                Guild.Id,
                textChannel.Id,
                currentTrackSnapshot?.Url
            );

            messageTasks = item switch
            {
                AudioEvent.Error => await HandleAudioErrorLockedAsync(
                    exception,
                    currentTrackSnapshot,
                    ct
                ),
                AudioEvent.Ended => await HandleAudioEndedLockedAsync(currentTrackSnapshot, ct),
                AudioEvent.None => [],
                _ => throw new UnreachableException($"Unknown audio event: {item}"),
            };
        }

        if (messageTasks.Count > 0)
        {
            await Task.WhenAll(messageTasks);
        }
    }

    private async Task<List<Task>> HandleAudioErrorLockedAsync(
        Exception? exception,
        Track? currentTrackSnapshot,
        CancellationToken ct
    )
    {
        var messageTasks = new List<Task>(capacity: 2);

        logger.LogError(
            exception,
            "Error in audio stream. GuildId={GuildId} TextChannelId={TextChannelId} CurrentTrack={CurrentTrackUrl}",
            Guild.Id,
            textChannel.Id,
            currentTrackSnapshot?.Url
        );

        var error = Error
            .Unexpected(
                code: "Audio.StreamError",
                description: "Playback failed. I'll try the next track."
            )
            .WithMetadata(SessionMetadata("audio.update", currentTrackSnapshot));

        if (exception is not null)
        {
            error = error.WithException(exception);
        }

        messageTasks.Add(SendTextAsync(((ErrorOr<Success>)error).ToErrorContent(), ct));

        // Clear current track so the next AudioUpdate reflects that we're trying to recover.
        Volatile.Write(ref _currentTrack, null);

        var nextFromError = await PlayNextTrackFromQueueAsync(true, ct);

        if (nextFromError.IsError)
        {
            logger.LogError(
                "Failed to play next track after stream error. GuildId={GuildId} TextChannelId={TextChannelId} ErrorCode={ErrorCode}",
                Guild.Id,
                textChannel.Id,
                nextFromError.FirstError.Code
            );

            messageTasks.Add(SendTextAsync(nextFromError.ToErrorContent(), ct));
            return messageTasks;
        }

        if (nextFromError.Value.Track is null)
        {
            messageTasks.Add(SendQueueEmptyAsync(ct));
        }

        return messageTasks;
    }

    private async Task<List<Task>> HandleAudioEndedLockedAsync(
        Track? currentTrackSnapshot,
        CancellationToken ct
    )
    {
        var messageTasks = new List<Task>(capacity: 1);

        logger.LogTrace(
            "Track ended. GuildId={GuildId} TextChannelId={TextChannelId} LastTrack={CurrentTrackUrl}",
            Guild.Id,
            textChannel.Id,
            currentTrackSnapshot?.Url
        );

        var next = await PlayNextTrackFromQueueAsync(true, ct);

        if (next.IsError)
        {
            logger.LogError(
                "Failed to play next track. GuildId={GuildId} TextChannelId={TextChannelId} ErrorCode={ErrorCode}",
                Guild.Id,
                textChannel.Id,
                next.FirstError.Code
            );

            messageTasks.Add(SendTextAsync(next.ToErrorContent(), ct));
            return messageTasks;
        }

        if (next.Value.Track is null)
        {
            messageTasks.Add(SendQueueEmptyAsync(ct));
        }

        return messageTasks;
    }

    private async Task SendQueueEmptyAsync(CancellationToken ct)
    {
        logger.LogTrace("No more tracks in queue. GuildId={GuildId}", Guild.Id);
        await SendTextAsync("Queue is empty. No more tracks to play.", ct);
    }

    private async Task SendTextAsync(string content, CancellationToken ct)
    {
        try
        {
            await textChannel.SendMessageAsync(
                new MessageProperties { Content = content },
                cancellationToken: ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown/dispose.
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send message. GuildId={GuildId} TextChannelId={TextChannelId}",
                Guild.Id,
                textChannel.Id
            );
        }
    }

    private async Task<ErrorOr<AudioUpdate>> PlayNextTrackFromQueueAsync(
        bool now,
        CancellationToken ct
    )
    {
        var status = await GuildVoiceSession.AudioPlayer.StatusAsync(ct);

        var currentTrackSnapshot = Volatile.Read(ref _currentTrack);

        if (currentTrackSnapshot is not null && !now && status.State != AudioState.Ended)
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

        var resolved = await ResolveTrackForYouTubePlaybackAsync(firstTrack, "queue.next", ct);

        if (resolved.IsError)
        {
            return resolved.Errors;
        }

        var trackToPlay = resolved.Value;

        var cache = await GetOrAddCacheFileAsync(trackToPlay, "queue.next.cache.getOrAdd", ct);

        if (cache.IsError)
        {
            return cache.Errors;
        }

        var cacheFile = cache.Value;

        if (!cacheFile.Exists())
        {
            var download = await EnsureDownloadedAsync(
                trackToPlay,
                cacheFile,
                "queue.next.youtube.download",
                ct
            );

            if (download.IsError)
            {
                return download.Errors;
            }
        }
        else
        {
            logger.LogDebug(
                "Playing cached track. GuildId={GuildId} TrackUrl={TrackUrl}",
                Guild.Id,
                trackToPlay.Url
            );
        }

        var play = await GuildVoiceSession.AudioPlayer.PlayAsync(cacheFile, UpdateAsync, ct);

        if (play.IsError)
        {
            return play.WithMetadata(SessionMetadata("queue.next.player.play", trackToPlay)).Errors;
        }

        Volatile.Write(ref _currentTrack, trackToPlay);
        DownloadNextTrackAsync(ct).FireAndForget(logger);
        return await BuildAudioUpdateAsync();
    }

    private async Task<AudioUpdate> BuildAudioUpdateAsync()
    {
        var status = await GuildVoiceSession.AudioPlayer.StatusAsync(CancellationToken.None);
        var nextTrack = _queue.TryPeek(out var next) ? next : null;
        var currentTrackSnapshot = Volatile.Read(ref _currentTrack);
        return new AudioUpdate(currentTrackSnapshot, nextTrack, status);
    }

    private async Task SendPrefetchErrorAsync(ErrorOr<Success> errorOr, CancellationToken ct)
    {
        var audioUpdate = await BuildAudioUpdateAsync();

        try
        {
            await gatewayClient.Rest.SendMessageAsync(
                textChannel.Id,
                new MessageProperties
                {
                    Content = $"""
                    {errorOr.ToErrorContent()}
                    {audioUpdate.ToValueContent()}
                    """,
                },
                cancellationToken: ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown/dispose.
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send prefetch error message. GuildId={GuildId} TextChannelId={TextChannelId}",
                Guild.Id,
                textChannel.Id
            );
        }
    }

    private async Task DownloadNextTrackAsync(CancellationToken ct)
    {
        if (!_queue.TryPeek(out var nextTrack))
        {
            return;
        }

        logger.LogDebug(
            "Pre-downloading next track. GuildId={GuildId} TextChannelId={TextChannelId} TrackUrl={TrackUrl}",
            Guild.Id,
            textChannel.Id,
            nextTrack.Url
        );

        var resolved = await ResolveTrackForYouTubePlaybackAsync(
            nextTrack,
            "queue.predownload",
            ct
        );

        if (resolved.IsError)
        {
            logger.LogError(
                "Failed to pre-download next track (resolve). GuildId={GuildId} TrackUrl={TrackUrl} ErrorCode={ErrorCode}",
                Guild.Id,
                nextTrack.Url,
                resolved.FirstError.Code
            );

            await SendPrefetchErrorAsync(resolved.Errors, ct);
            return;
        }

        nextTrack = resolved.Value;

        var nextCache = await GetOrAddCacheFileAsync(
            nextTrack,
            "queue.predownload.cache.getOrAdd",
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

            await SendPrefetchErrorAsync(nextCache.Errors, ct);
            return;
        }

        if (!nextCache.Value.Exists())
        {
            var download = await EnsureDownloadedAsync(
                nextTrack,
                nextCache.Value,
                "queue.predownload.youtube.download",
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

                await SendPrefetchErrorAsync(download.Errors, ct);
                return;
            }
        }

        logger.LogDebug(
            "Pre-download completed. GuildId={GuildId} TrackUrl={TrackUrl}",
            Guild.Id,
            nextTrack.Url
        );
    }

    private ByteSize ApproxCacheSize(Track track)
    {
        return Pcm16Bytes.ToBytes(track.Duration * CacheSizeSlackFactor).Humanize();
    }

    private async Task<ErrorOr<Track>> ResolveTrackForYouTubePlaybackAsync(
        Track track,
        string operationBase,
        CancellationToken ct
    )
    {
        if (!spotifySearch.IsSpotifyQuery(track.Url))
        {
            return track;
        }

        logger.LogDebug(
            "Spotify track detected, searching on YouTube. GuildId={GuildId} TrackName={TrackName} TrackArtists={TrackArtists}",
            Guild.Id,
            track.Name,
            track.Artists
        );

        var search = await youtubeSearch.SearchAsync($"{track.Name} {track.Artists}", ct);

        if (search.IsError)
        {
            return search
                .WithMetadata(SessionMetadata($"{operationBase}.spotify.youtubeSearch", track))
                .Errors;
        }

        if (search.Value.Count == 0)
        {
            if (operationBase == "queue.predownload")
            {
                logger.LogWarning(
                    "Did not find next track during pre-download. GuildId={GuildId} TrackName={TrackName} TrackArtists={TrackArtists}",
                    Guild.Id,
                    track.Name,
                    track.Artists
                );

                return Error
                    .NotFound(description: "Did not find next track")
                    .WithMetadata(SessionMetadata($"{operationBase}.spotify.youtubeSearch", track));
            }

            return Error
                .NotFound(description: "Did not find next track")
                .WithMetadata(SessionMetadata($"{operationBase}.spotify.youtubeSearch", track));
        }

        var yt = search.Value.First();
        var updatedTrack = YouTubeTrackMapper.ToTrack(yt);

        var update = await musicCache.AddOrUpdateTrackAsync(
            track,
            updatedTrack,
            ApproxCacheSize(updatedTrack),
            ct
        );

        if (update.IsError)
        {
            return update
                .WithMetadata(
                    SessionMetadata($"{operationBase}.spotify.cache.update", updatedTrack)
                )
                .Errors;
        }

        return updatedTrack;
    }

    private async Task<ErrorOr<IFileInfo>> GetOrAddCacheFileAsync(
        Track track,
        string operation,
        CancellationToken ct
    )
    {
        var cache = await musicCache.GetOrAddTrackAsync(track, ApproxCacheSize(track), ct);

        if (cache.IsError)
        {
            return cache.WithMetadata(SessionMetadata(operation, track)).Errors;
        }

        return ErrorOrFactory.From(cache.Value);
    }

    private async Task<ErrorOr<Success>> EnsureDownloadedAsync(
        Track track,
        IFileInfo output,
        string operation,
        CancellationToken ct
    )
    {
        var download = await youTubeDownload.DownloadAsync(
            $"{track.Name} {track.Artists}",
            output,
            ct
        );

        if (download.IsError)
        {
            return download.WithMetadata(SessionMetadata(operation, track)).Errors;
        }

        return Result.Success;
    }

    private Dictionary<string, object?> SessionMetadata(
        string operation,
        Track? track = null,
        string? query = null
    )
    {
        var metadata = new Dictionary<string, object?>
        {
            [ErrorExtensions.MetadataKeys.Operation] = operation,
            ["guild.id"] = Guild.Id,
            ["textChannel.id"] = textChannel.Id,
        };

        if (track is not null)
        {
            metadata["track.url"] = track.Url;
            metadata["track.name"] = track.Name;
            metadata["track.artists"] = track.Artists;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            metadata["query"] = query;
        }

        return metadata;
    }
}
