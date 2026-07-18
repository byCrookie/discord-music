using System.IO.Abstractions;
using DiscordMusic.Core.Audio.Sending;

namespace DiscordMusic.Core.Audio.Sources;

internal sealed class FilePcmAudioSourceFactory : IPcmAudioSourceFactory
{
    public ValueTask<Stream> OpenAsync(
        IFileInfo inputFile,
        TimeSpan startPosition,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var input = inputFile.OpenRead();
        if (input.CanSeek)
        {
            input.Position = TimedAudioSender.CalculateByteOffset(startPosition);
        }

        return ValueTask.FromResult<Stream>(input);
    }
}
