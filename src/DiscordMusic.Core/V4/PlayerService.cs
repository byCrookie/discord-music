using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.V4;

public class PlayerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var track in _queue.PlaybackRequests.ReadAllAsync(stoppingToken))
        {
            // 1. Create a linked token (Service Stop OR User Skip)
            using var trackCts = _queue.GetTrackCancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, trackCts.Token);

            try 
            {
                await PlayAudioAsync(track, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // This is expected when a user skips!
                // Do any cleanup here (e.g., tell Discord to stop speaking)
            }
        }
    }
    
    private async Task PlayAudioAsync(MusicTrack track, CancellationToken ct)
    {
        using var ffmpeg = CreateFfmpegProcess(track.LocalFilePath);
        using var output = ffmpeg.StandardOutput.BaseStream;
        using var discordStream = _voiceClient.CreateOpusStream();

        byte[] buffer = new byte[3840]; // 20ms of audio
        int bytesRead;

        // The loop checks the token on every chunk
        while ((bytesRead = await output.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            // WriteAsync also accepts the token to handle Discord-side backpressure
            await discordStream.WriteAsync(buffer, 0, bytesRead, ct);
        }
    }
}
