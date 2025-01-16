using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord.Interactions;

public class AudioBarModule(IAudioPlayer audioPlayer) : ComponentInteractionModule<ButtonInteractionContext>
{
    public const string FastBackwardButton = "fastBackward";
    public const string BackwardButton = "backward";
    public const string PlayPauseButton = "playpause";
    public const string FowardButton = "forward";
    public const string FastForwardButton = "fastForward";

    [ComponentInteraction(FastBackwardButton)]
    public string FastBackward()
    {
        var duration = TimeSpan.FromSeconds(30);

        var action = audioPlayer
            .SeekAsync(duration, AudioStream.SeekMode.Backward, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return action.IsError
            ? action.ToPrint()
            : $"Seeked backward by {duration.HummanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(BackwardButton)]
    public string Backward()
    {
        var duration = TimeSpan.FromSeconds(10);

        var action = audioPlayer
            .SeekAsync(duration, AudioStream.SeekMode.Backward, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return action.IsError
            ? action.ToPrint()
            : $"Seeked backward by {duration.HummanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(PlayPauseButton)]
    public string PlayPause()
    {
        var isPlaying = audioPlayer.IsPlayingAsync(CancellationToken.None).GetAwaiter().GetResult();

        if (isPlaying)
        {
            var pause = audioPlayer.PauseAsync(CancellationToken.None).GetAwaiter().GetResult();
            return pause.IsError ? pause.ToPrint() : $"Paused. Now at {StatusHumanReadable(pause.Value)}";
        }

        var resume = audioPlayer.ResumeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return resume.IsError ? resume.ToPrint() : $"Resumed. Now at {StatusHumanReadable(resume.Value)}";
    }

    [ComponentInteraction(FowardButton)]
    public string Foward()
    {
        var duration = TimeSpan.FromSeconds(10);

        var action = audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Forward, CancellationToken.None).GetAwaiter()
            .GetResult();

        return action.IsError
            ? action.ToPrint()
            : $"Seeked forward by {duration.HummanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(FastForwardButton)]
    public string FastForward()
    {
        var duration = TimeSpan.FromSeconds(30);

        var action = audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Forward, CancellationToken.None).GetAwaiter()
            .GetResult();

        return action.IsError
            ? action.ToPrint()
            : $"Seeked forward by {duration.HummanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    private static string StatusHumanReadable(AudioStatus status)
    {
        return $"{status.Position.HummanizeSecond()} / {status.Length.HummanizeSecond()}";
    }
}
