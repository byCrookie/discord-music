using Humanizer;

namespace DiscordMusic.Core.Utils;

public static class TimeSpanExtensions
{
    extension(TimeSpan timeSpan)
    {
        public string HumanizeSecond()
        {
            return timeSpan.Humanize(
                2,
                maxUnit: TimeUnit.Hour,
                minUnit: TimeUnit.Second,
                collectionSeparator: " "
            );
        }

        public string HumanizeMillisecond()
        {
            return timeSpan.Humanize(
                2,
                maxUnit: TimeUnit.Hour,
                minUnit: TimeUnit.Millisecond,
                collectionSeparator: " "
            );
        }
    }
}
