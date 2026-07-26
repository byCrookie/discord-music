using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Audio.Sending;
using DiscordMusic.Core.Observability;

namespace DiscordMusic.Core.Audio.Sources;

internal sealed class FilePcmAudioSourceFactory : IPcmAudioSourceFactory
{
    public ValueTask<Stream> OpenAsync(
        IFileInfo inputFile,
        TimeSpan startPosition,
        CancellationToken cancellationToken
    )
    {
        using var activity = DiscordMusicObservability.StartActivity("audio.source.open");
        DiscordMusicObservability.SetTag(
            activity,
            "music.playback.start_position_ms",
            startPosition.TotalMilliseconds
        );

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = inputFile.OpenRead();
            if (input.CanSeek)
            {
                input.Position = TimedAudioSender.CalculateByteOffset(startPosition);
            }

            DiscordMusicObservability.SetTag(activity, "file.seekable", input.CanSeek);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return ValueTask.FromResult<Stream>(input);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
