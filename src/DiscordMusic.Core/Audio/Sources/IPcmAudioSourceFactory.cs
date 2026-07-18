using System.IO.Abstractions;

namespace DiscordMusic.Core.Audio.Sources;

internal interface IPcmAudioSourceFactory
{
    ValueTask<Stream> OpenAsync(
        IFileInfo inputFile,
        TimeSpan startPosition,
        CancellationToken cancellationToken
    );
}
