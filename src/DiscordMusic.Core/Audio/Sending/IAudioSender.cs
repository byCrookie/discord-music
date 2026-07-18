using System.IO.Abstractions;
using DiscordMusic.Core.Playback;
using DiscordMusic.Core.Tracks;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Audio.Sending;

internal interface IAudioSender
{
    Task SendAsync(
        VoiceClient voiceClient,
        Track track,
        IFileInfo inputFile,
        TimeSpan startPosition,
        PlaybackSession playbackSession,
        CancellationToken cancellationToken
    );
}
