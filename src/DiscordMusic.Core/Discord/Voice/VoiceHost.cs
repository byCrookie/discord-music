using System.Diagnostics;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Queue;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.Voice;

public class VoiceHost(
    Replier replier,
    IAudioPlayer audioPlayer,
    GatewayClient gatewayClient,
    ILogger<VoiceHost> logger,
    IYoutubeSearch youtubeSearch,
    IYouTubeDownload youTubeDownload,
    IMusicCache musicCache,
    IQueue<Track> musicQueue,
    ISpotifySearch spotifySearch
) : IVoiceHost
{
    private readonly AsyncLock _lock = new();
    private VoiceConnection? _connection;
    private Track? _currentTrack;

    public async Task<ErrorOr<Success>> ConnectAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Connect");
        await using var _ = await _lock.AquireAsync(ct);

        if (!message.GuildId.HasValue)
        {
            return Error.Validation(description: "Message does not have a guild id");
        }

        var guild = message.Guild!;
        var userId = message.Author.Id;

        if (!guild.VoiceStates.TryGetValue(userId, out var userVoiceState))
        {
            return Error.Validation(description: "You are not in a voice channel");
        }

        var botId = gatewayClient.Cache.User!.Id;
        var channelId = userVoiceState.ChannelId!.Value;
        var guildId = message.GuildId.Value;

        if (guild.VoiceStates.TryGetValue(botId, out var botVoiceState))
        {
            if (botVoiceState.ChannelId == userVoiceState.ChannelId && _connection is not null)
            {
                logger.LogTrace("Bot is already in the same voice channel");
                return Result.Success;
            }
        }

        if (_connection is not null)
        {
            logger.LogTrace("Disconnecting from voice channel {ChannelId}", _connection.ChannelId);
            await audioPlayer.StopAsync(ct);
            await _connection.CloseAsync(ct);
            _connection = null;
            _currentTrack = null;
        }

        logger.LogInformation("Joining voice channel {ChannelId}", channelId);
        var voiceClient = await gatewayClient.JoinVoiceChannelAsync(guildId, channelId, cancellationToken: ct);
        await voiceClient.StartAsync(ct);
        await voiceClient.EnterSpeakingStateAsync(SpeakingFlags.Priority, cancellationToken: ct);
        var opusStream = new OpusEncodeStream(
            voiceClient.CreateOutputStream(),
            PcmFormat.Short,
            VoiceChannels.Stereo,
            OpusApplication.Audio
        );

        _connection = new VoiceConnection(voiceClient, guildId, channelId, opusStream);
        await audioPlayer.StartAsync(_connection!.Output, UpdateAsync, ct);
        return Result.Success;
    }

    public async Task<ErrorOr<Success>> DisconnectAsync(CancellationToken ct)
    {
        logger.LogTrace("Disconnect");
        await using var _ = await _lock.AquireAsync(ct);

        if (_connection is null)
        {
            return Result.Success;
        }

        logger.LogTrace("Disconnecting from voice channel {ChannelId}", _connection.ChannelId);
        await audioPlayer.StopAsync(ct);
        await _connection.CloseAsync(ct);
        _connection = null;
        _currentTrack = null;

        await gatewayClient.CloseAsync(cancellationToken: ct);
        await gatewayClient.StartAsync(cancellationToken: ct);
        return Result.Success;
    }

    public async Task<ErrorOr<VoiceUpdate>> PlayAsync(Message message, string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        return await PlayFromQueryAsync(message, query, true, ct);
    }

    public async Task<ErrorOr<VoiceUpdate>> PlayNextAsync(Message message, string query, CancellationToken ct)
    {
        logger.LogTrace("Play");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        return await PlayFromQueryAsync(message, query, false, ct);
    }

    public async Task<ErrorOr<Success>> QueueClearAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Queue");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        musicQueue.Clear();
        return Result.Success;
    }

    public async Task<ErrorOr<VoiceUpdate>> SeekAsync(
        Message message,
        TimeSpan time,
        AudioStream.SeekMode mode,
        CancellationToken ct
    )
    {
        logger.LogTrace("Seek {Mode}", mode);
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        var seek = await audioPlayer.SeekAsync(time, mode, ct);

        if (seek.IsError)
        {
            return seek.Errors;
        }

        return new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, seek.Value);
    }

    public async Task<ErrorOr<VoiceUpdate>> ShuffleAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Shuffle");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        musicQueue.Shuffle();
        DownloadNextTrackInBackgroud(ct);

        return musicQueue.TryPeek(out var track)
            ? new VoiceUpdate(VoiceUpdateType.Next, track, await audioPlayer.StatusAsync(ct))
            : VoiceUpdate.None(VoiceUpdateType.Next);
    }

    public async Task<ErrorOr<VoiceUpdate>> SkipAsync(Message message, int toIndex, CancellationToken ct)
    {
        logger.LogTrace("Skip");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        musicQueue.SkipTo(toIndex);
        return await PlayNextTrackFromQueueAsync(true, ct);
    }

    public async Task<ErrorOr<ICollection<Track>>> QueueAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Queue");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        return ErrorOrFactory.From(musicQueue.Items());
    }

    public async Task<ErrorOr<VoiceUpdate>> PauseAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Pause");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        var pause = await audioPlayer.PauseAsync(ct);

        if (pause.IsError)
        {
            return pause.Errors;
        }

        return new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, pause.Value);
    }

    public async Task<ErrorOr<VoiceUpdate>> ResumeAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Resume");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        var resume = await audioPlayer.ResumeAsync(ct);

        if (resume.IsError)
        {
            return resume.Errors;
        }

        return new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, resume.Value);
    }

    public async Task<ErrorOr<VoiceUpdate>> NowPlayingAsync(Message message, CancellationToken ct)
    {
        logger.LogTrace("Now Playing");
        var connect = await ConnectAsync(message, ct);

        if (connect.IsError)
        {
            return connect.Errors;
        }

        return _currentTrack is null
            ? VoiceUpdate.None(VoiceUpdateType.Now)
            : new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, await audioPlayer.StatusAsync(ct));
    }

    private async Task<ErrorOr<VoiceUpdate>> PlayFromQueryAsync(
        Message message,
        string query,
        bool append,
        CancellationToken ct
    )
    {
        await replier
            .Reply()
            .To(message)
            .WithEmbed($"Searching for {query}", "This may take a moment...")
            .WithDeletion()
            .SendAsync(ct);

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
                .Value.Select(track => new Track(track.Name, track.Artists, track.Url, TimeSpan.Zero))
                .ToList();

            foreach (var track in spotifyTracks)
            {
                if (append)
                {
                    musicQueue.EnqueueLast(track);
                }
                else
                {
                    musicQueue.EnqueueFirst(track);
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
                musicQueue.EnqueueLast(track);
            }
            else
            {
                musicQueue.EnqueueFirst(track);
            }
        }

        return await PlayNextTrackFromQueueAsync(false, ct);
    }

    private async Task UpdateAsync(AudioEvent item, Exception? exception, CancellationToken ct)
    {
        logger.LogTrace("Update {Item}", item);

        switch (item)
        {
            case AudioEvent.Error:
                logger.LogError(exception, "Error in audio stream");
                await replier
                    .Reply()
                    .To(_connection!.ChannelId)
                    .SendErrorAsync("Error in audio stream. Trying to play the next track.", ct);
                _currentTrack = null;

                var nextFromError = await PlayNextTrackFromQueueAsync(true, ct);

                if (nextFromError.IsError)
                {
                    logger.LogError("Failed to play next track: {Error}", nextFromError.ToPrint());
                    await replier.Reply().To(_connection!.ChannelId).SendErrorAsync(nextFromError.ToPrint(), ct);
                    return;
                }

                if (nextFromError.Value.Track is null)
                {
                    logger.LogTrace("No more tracks in queue");

                    await replier
                        .Reply()
                        .To(_connection!.ChannelId)
                        .WithEmbed("Queue empty", "No more tracks in queue")
                        .WithDeletion()
                        .SendAsync(ct);
                }

                return;
            case AudioEvent.Ended:
            {
                logger.LogTrace("Track ended");
                var next = await PlayNextTrackFromQueueAsync(true, ct);

                if (next.IsError)
                {
                    logger.LogError("Failed to play next track: {Error}", next.ToPrint());
                    await replier.Reply().To(_connection!.ChannelId).SendErrorAsync(next.ToPrint(), ct);
                    return;
                }

                if (next.Value.Track is null)
                {
                    logger.LogTrace("No more tracks in queue");

                    await replier
                        .Reply()
                        .To(_connection!.ChannelId)
                        .WithEmbed("Queue empty", "No more tracks in queue")
                        .WithDeletion()
                        .SendAsync(ct);
                }

                break;
            }
            case AudioEvent.None:
                break;
            default:
                throw new UnreachableException($"Unknown audio event: {item}");
        }
    }

    private async Task<ErrorOr<VoiceUpdate>> PlayNextTrackFromQueueAsync(bool now, CancellationToken ct)
    {
        var status = await audioPlayer.StatusAsync(ct);

        if (_currentTrack is not null && !now && status.State != AudioState.Ended)
        {
            DownloadNextTrackInBackgroud(ct);
            return musicQueue.TryPeek(out var nextTrack)
                ? new VoiceUpdate(VoiceUpdateType.Next, nextTrack, status)
                : VoiceUpdate.None(VoiceUpdateType.Next);
        }

        if (!musicQueue.TryDequeue(out var firstTrack))
        {
            return VoiceUpdate.None(VoiceUpdateType.Next);
        }

        if (spotifySearch.IsSpotifyQuery(firstTrack.Url))
        {
            logger.LogDebug("Use YouTube to search for Spotify track {Name} {Artists}", firstTrack.Name,
                firstTrack.Artists);

            var search = await youtubeSearch.SearchAsync($"{firstTrack.Name} {firstTrack.Artists}", ct);

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

            var update =
                await musicCache.AddOrUpdateTrackAsync(firstTrack, track,
                    AudioStream.ApproxSize(track.Duration * (5d / 4d)),
                    ct);

            if (update.IsError)
            {
                return update.Errors;
            }

            firstTrack = track;
        }

        var cache = await musicCache.GetOrAddTrackAsync(firstTrack,
            AudioStream.ApproxSize(firstTrack.Duration * (5d / 4d)),
            ct);

        if (cache.IsError)
        {
            return cache.Errors;
        }

        if (cache.Value.Exists())
        {
            logger.LogDebug("Playing existing track {Name} {Artists}", firstTrack.Name, firstTrack.Artists);

            var playExisting = await audioPlayer.PlayAsync(cache.Value, ct);

            if (playExisting.IsError)
            {
                return playExisting.Errors;
            }

            _currentTrack = firstTrack;
            DownloadNextTrackInBackgroud(ct);
            return new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, await audioPlayer.StatusAsync(ct));
        }

        var download = await youTubeDownload.DownloadAsync($"{firstTrack.Name} {firstTrack.Artists}", cache.Value, ct);

        if (download.IsError)
        {
            return download.Errors;
        }

        var play = await audioPlayer.PlayAsync(cache.Value, ct);

        if (play.IsError)
        {
            return play.Errors;
        }

        _currentTrack = firstTrack;
        DownloadNextTrackInBackgroud(ct);
        return new VoiceUpdate(VoiceUpdateType.Now, _currentTrack, await audioPlayer.StatusAsync(ct));
    }

    private void DownloadNextTrackInBackgroud(CancellationToken ct)
    {
        _ = Task.Run(
            async () =>
            {
                if (musicQueue.TryPeek(out var nextTrack))
                {
                    logger.LogDebug("Downloading next track {Name} {Artists} in the background", nextTrack.Name,
                        nextTrack.Artists);

                    if (spotifySearch.IsSpotifyQuery(nextTrack.Url))
                    {
                        var search = await youtubeSearch.SearchAsync($"{nextTrack.Name} {nextTrack.Artists}", ct);

                        if (search.IsError)
                        {
                            logger.LogError("Failed to download next track: {Error}", search.ToPrint());
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

                        var update = await musicCache.AddOrUpdateTrackAsync(nextTrack, track,
                            AudioStream.ApproxSize(track.Duration * (5d / 4d)), ct);

                        if (update.IsError)
                        {
                            logger.LogError("Failed to update next track: {Error}", update.ToPrint());
                            return;
                        }

                        nextTrack = track;
                    }

                    var nextCache =
                        await musicCache.GetOrAddTrackAsync(nextTrack,
                            AudioStream.ApproxSize(nextTrack.Duration * (5d / 4d)),
                            ct);

                    if (nextCache.IsError)
                    {
                        logger.LogError("Failed to get or add next track to cache: {Error}", nextCache.ToPrint());
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
                            logger.LogError("Failed to download next track: {Error}", download.ToPrint());
                            return;
                        }
                    }

                    logger.LogDebug("Downloaded next track {Name} {Artists} in the background", nextTrack.Name,
                        nextTrack.Artists);
                }
            },
            ct
        );
    }
}
