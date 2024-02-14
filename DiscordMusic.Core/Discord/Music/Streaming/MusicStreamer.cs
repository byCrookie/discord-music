using System.Diagnostics;
using System.IO.Abstractions;
using Discord;
using Discord.Audio;
using DiscordMusic.Core.Discord.Music.Download;
using DiscordMusic.Core.Discord.Music.Queue;
using DiscordMusic.Core.Discord.Options;
using DiscordMusic.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Discord.Music.Streaming;

internal class MusicStreamer(
    IMusicQueue queue,
    IMusicDownloader downloader,
    ILogger<MusicStreamer> logger,
    IOptions<DiscordOptions> discordOptions)
    : IMusicStreamer
{
    private IAudioClient? _audioClient;
    private IVoiceChannel? _channel;

    private CancellationTokenSource? _cts;
    private IDiscordClient? _discordClient;

    private CancellationTokenSource? _pauseCts;

    public Track? CurrentTrack { get; private set; }

    public async Task ConnectAsync(IDiscordClient client, IVoiceChannel channel)
    {
        if (_channel is not null)
        {
            logger.LogDebug("Already connected to voice channel {Channel}.", _channel.Name);
            return;
        }

        logger.LogDebug("Connect to voice channel {Channel}.", channel.Name);

        _discordClient ??= client;
        _channel ??= channel;
        _audioClient ??= await channel.ConnectAsync();

        _cts ??= new CancellationTokenSource();
        _pauseCts ??= new CancellationTokenSource();
        await SetSpeakingAsync(true);
    }

    public async Task DisconnectAsync()
    {
        logger.LogDebug("Disconnect from voice channel {Channel}.", _channel?.Name);
        await SetSpeakingAsync(false);

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        if (_pauseCts is not null)
        {
            await _pauseCts.CancelAsync();
            _pauseCts.Dispose();
            _pauseCts = null;
        }

        if (_channel is not null)
        {
            await _channel.DisconnectAsync();
            _channel = null;
        }

        _audioClient = null;
        _discordClient = null;
    }

    public async Task PlayAsync(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            logger.LogDebug("No argument provided. Resume current track.");
            _pauseCts ??= new CancellationTokenSource();
            await SetSpeakingAsync(true);
            return;
        }

        logger.LogDebug("Play {Argument}", argument);
        Prepare(argument, (tracks, q) =>
        {
            foreach (var track in tracks)
            {
                q.Enqueue(track);
            }
        });

        _pauseCts ??= new CancellationTokenSource();
        await SetSpeakingAsync(true);
    }

    public async Task PlayNextAsync(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            logger.LogDebug("No argument provided. Resume current track.");
            _pauseCts ??= new CancellationTokenSource();
            await SetSpeakingAsync(true);
            return;
        }

        logger.LogDebug("Play next {Argument}", argument);
        Prepare(argument, (tracks, q) =>
        {
            foreach (var track in tracks.Reverse())
            {
                q.EnqueueNext(track);
            }
        });

        _pauseCts ??= new CancellationTokenSource();
        await SetSpeakingAsync(true);
    }

    public async Task SkipAsync()
    {
        logger.LogDebug("Skip current track.");
        await SetSpeakingAsync(false);

        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        _pauseCts ??= new CancellationTokenSource();
        await SetSpeakingAsync(true);
    }

    public async Task PauseAsync()
    {
        if (_pauseCts is not null)
        {
            logger.LogDebug("Pause current track.");
            await SetSpeakingAsync(false);
            await _pauseCts.CancelAsync();
            _pauseCts.Dispose();
            _pauseCts = null;
        }
        else
        {
            logger.LogDebug("Resume current track.");
            await SetSpeakingAsync(true);
            _pauseCts ??= new CancellationTokenSource();
        }
    }

    public bool CanExecute()
    {
        if (_cts?.Token is null)
        {
            return false;
        }

        if (_audioClient?.ConnectionState != ConnectionState.Connected)
        {
            return false;
        }

        if (!queue.TryPeek(out _))
        {
            return false;
        }

        return true;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (!queue.TryDequeue(out var track))
        {
            return;
        }

        CurrentTrack = track;

        if (!downloader.TryDownload(track!, out var file))
        {
            return;
        }

        var streamProcess = CreateStream(file!);

        DownloadNextTrackAsync(ct);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts!.Token);

        try
        {
            await SendAsync(_audioClient!, streamProcess.StandardOutput.BaseStream, cts.Token);
        }
        catch (OperationCanceledException)
        {
            streamProcess.StandardOutput.BaseStream.Close();
            streamProcess.StandardOutput.Close();
            streamProcess.Close();
            streamProcess.Dispose();
        }
        finally
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            cts.Dispose();
        }
    }

    private void DownloadNextTrackAsync(CancellationToken ct)
    {
        Task.Run(() =>
        {
            logger.LogDebug("Pre-download next track.");

            if (!queue.TryPeek(out var preTrack))
            {
                return;
            }

            downloader.TryDownload(preTrack!, out _);
        }, ct).FireAndForget();
    }

    private void Prepare(string? argument, Action<IEnumerable<Track>, IMusicQueue> enqueue)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new UnreachableException();
        }

        if (!downloader.TryPrepare(argument, out var tracks))
        {
            throw new Exception("Failed to prepare tracks.");
        }

        logger.LogDebug("Enqueue {Count} tracks.", tracks.Count);
        foreach (var track in tracks)
        {
            logger.LogTrace("Enqueue track: {Track} - {Author}", track.Title, track.Author);
        }

        enqueue(tracks, queue);
    }

    private async Task SendAsync(IAudioClient client, Stream stream, CancellationToken ct)
    {
        logger.LogDebug("Send stream to discord.");
        await using var output = stream;
        logger.LogTrace("Create PCM stream.");
        await using var discord = client.CreatePCMStream(AudioApplication.Mixed);

        while (!ct.IsCancellationRequested)
        {
            if (_pauseCts is null)
            {
                logger.LogTrace("Waiting for unpause.");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _pauseCts?.Token ?? CancellationToken.None);

            try
            {
                if (_pauseCts is not null && output.CanRead)
                {
                    logger.LogTrace("Copy PCM stream to discord.");
                    await output.CopyToAsync(discord, cts.Token);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                cts.Dispose();
            }
        }

        logger.LogDebug("Stop sending stream to discord.");
    }

    private Process CreateStream(IFileInfo file)
    {
        logger.LogTrace("Create stream for {File}", file.FullName);
        var command = $"-hide_banner -loglevel panic -i \"{file}\" -ac 2 -f s16le -ar 48000 pipe:1";
        logger.LogDebug("Run command {Command}", command);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = discordOptions.Value.Ffmpeg,
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true
        });

        if (process is null)
        {
            throw new Exception($"Failed to create stream from {file.FullName}.");
        }

        return process;
    }

    private async Task SetSpeakingAsync(bool speaking)
    {
        if (_audioClient is null)
        {
            return;
        }

        logger.LogDebug("Set speaking to {Speaking}", speaking);
        await _audioClient.SetSpeakingAsync(speaking);
        await Task.Delay(TimeSpan.FromSeconds(0.1));
    }
}
