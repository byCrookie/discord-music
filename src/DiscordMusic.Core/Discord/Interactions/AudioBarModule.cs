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
    public async Task<string> FastBackward()
    {
        var duration = TimeSpan.FromSeconds(30);

        var action = await audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Backward, CancellationToken.None);

        return action.IsError
            ? action.ToPrint()
            : $"Seeked backward by {duration.HumanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(BackwardButton)]
    public async Task<string> Backward()
    {
        var duration = TimeSpan.FromSeconds(10);

        var action = await audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Backward, CancellationToken.None);

        return action.IsError
            ? action.ToPrint()
            : $"Seeked backward by {duration.HumanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(PlayPauseButton)]
    public async Task<string> PlayPause()
    {
        var isPlaying = await audioPlayer.IsPlayingAsync(CancellationToken.None);

        if (isPlaying)
        {
            var pause = await audioPlayer.PauseAsync(CancellationToken.None);
            return pause.IsError ? pause.ToPrint() : $"Paused. Now at {StatusHumanReadable(pause.Value)}";
        }

        var resume = await audioPlayer.ResumeAsync(CancellationToken.None);
        return resume.IsError ? resume.ToPrint() : $"Resumed. Now at {StatusHumanReadable(resume.Value)}";
    }

    [ComponentInteraction(FowardButton)]
    public async Task<string> Foward()
    {
        var duration = TimeSpan.FromSeconds(10);

        var action = await audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Forward, CancellationToken.None);

        return action.IsError
            ? action.ToPrint()
            : $"Seeked forward by {duration.HumanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    [ComponentInteraction(FastForwardButton)]
    public async Task<string> FastForward()
    {
        var duration = TimeSpan.FromSeconds(30);

        var action = await audioPlayer.SeekAsync(duration, AudioStream.SeekMode.Forward, CancellationToken.None);

        return action.IsError
            ? action.ToPrint()
            : $"Seeked forward by {duration.HumanizeSecond()}. Now at {StatusHumanReadable(action.Value)}";
    }

    private static string StatusHumanReadable(AudioStatus status)
    {
        return $"{status.Position.HumanizeSecond()} / {status.Length.HumanizeSecond()}";
    }
}
