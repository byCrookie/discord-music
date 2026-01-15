using NetCord;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.Interactions;

public static class AudioBar
{
    public static IMessageComponentProperties Create()
    {
        var fastBackward = new ButtonProperties(
            AudioBarModule.FastBackwardButton,
            "⏮️",
            ButtonStyle.Secondary
        );
        var backward = new ButtonProperties(
            AudioBarModule.BackwardButton,
            "⏪",
            ButtonStyle.Secondary
        );
        var playPause = new ButtonProperties(
            AudioBarModule.PlayPauseButton,
            "⏯️",
            ButtonStyle.Secondary
        );
        var forward = new ButtonProperties(AudioBarModule.FowardButton, "⏩", ButtonStyle.Secondary);
        var fastForward = new ButtonProperties(
            AudioBarModule.FastForwardButton,
            "⏭️",
            ButtonStyle.Secondary
        );
        return new ActionRowProperties { fastBackward, backward, playPause, forward, fastForward };
    }
}
