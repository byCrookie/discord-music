using NetCord;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal static class AudioBarComponents
{
    public const string Rewind30 = "audioRewind30";
    public const string Rewind10 = "audioRewind10";
    public const string PlayPause = "audioPlayPause";
    public const string Forward10 = "audioForward10";
    public const string Forward30 = "audioForward30";

    public static IMessageComponentProperties Create()
    {
        return new ActionRowProperties
        {
            new ButtonProperties(Rewind30, "⏮️", ButtonStyle.Secondary),
            new ButtonProperties(Rewind10, "⏪", ButtonStyle.Secondary),
            new ButtonProperties(PlayPause, "⏯️", ButtonStyle.Secondary),
            new ButtonProperties(Forward10, "⏩", ButtonStyle.Secondary),
            new ButtonProperties(Forward30, "⏭️", ButtonStyle.Secondary),
        };
    }
}
