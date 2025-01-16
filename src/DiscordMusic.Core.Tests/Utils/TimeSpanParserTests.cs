using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.Tests.Utils;

public class TimeSpanParserTests
{
    [Test]
    [MethodDataSource(nameof(DataSource))]
    public async Task ParseTimeSpan(string value, TimeSpan expectedTimeSpan, bool expectedCanParse)
    {
        var canParse = TimeSpanParser.TryParse(value, out var timeSpan);

        await Assert.That(canParse).IsEqualTo(expectedCanParse);
        await Assert.That(timeSpan).IsEqualTo(expectedTimeSpan);
    }

    public static IEnumerable<(string value, TimeSpan timeSpan, bool canParse)> DataSource()
    {
        yield return ("1", TimeSpan.FromSeconds(1), true);
        yield return ("1:0", TimeSpan.FromMinutes(1), true);
        yield return ("01:00", TimeSpan.FromMinutes(1), true);
        yield return ("1:0:0", TimeSpan.FromHours(1), true);
        yield return ("01:00:00", TimeSpan.FromHours(1), true);
        yield return ("1:0:0:0", TimeSpan.FromDays(1), true);
        yield return ("01:00:00:00", TimeSpan.FromDays(1), true);
    }
}
