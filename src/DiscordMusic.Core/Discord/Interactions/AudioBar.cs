using NetCord;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.Interactions;

public static class AudioBar
{
    public static ComponentProperties Create()
    {
        var fastBackward = new ButtonProperties(AudioBarModule.FastBackwardButton, "⏮️", ButtonStyle.Secondary);
        var backward = new ButtonProperties(AudioBarModule.BackwardButton, "⏪", ButtonStyle.Secondary);
        var playpause = new ButtonProperties(AudioBarModule.PlayPauseButton, "⏯️", ButtonStyle.Secondary);
        var forward = new ButtonProperties(AudioBarModule.FowardButton, "⏩", ButtonStyle.Secondary);
        var fastForward = new ButtonProperties(AudioBarModule.FastForwardButton, "⏭️", ButtonStyle.Secondary);
        return new ActionRowProperties().WithButtons([fastBackward, backward, playpause, forward, fastForward]);
    }
}
