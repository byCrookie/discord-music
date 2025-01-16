using Humanizer;
using Humanizer.Localisation;

namespace DiscordMusic.Core.Utils;

public static class TimeSpanExtensions
{
    public static string HummanizeSecond(this TimeSpan timeSpan)
    {
        return timeSpan.Humanize(2, maxUnit: TimeUnit.Hour, minUnit: TimeUnit.Second, collectionSeparator: " ");
    }

    public static string HummanizeMillisecond(this TimeSpan timeSpan)
    {
        return timeSpan.Humanize(2, maxUnit: TimeUnit.Hour, minUnit: TimeUnit.Millisecond, collectionSeparator: " ");
    }
}
