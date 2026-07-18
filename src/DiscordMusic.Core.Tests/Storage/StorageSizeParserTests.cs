using DiscordMusic.Core.Storage;

namespace DiscordMusic.Core.Tests.Storage;

public class StorageSizeParserTests
{
    [Test]
    [MethodDataSource(nameof(DataSource))]
    public async Task ParseStorageSize(string value, long expectedBytes, bool expectedCanParse)
    {
        var canParse = StorageSizeParser.TryParseBytes(value, out var bytes);

        await Assert.That(canParse).IsEqualTo(expectedCanParse);
        await Assert.That(bytes).IsEqualTo(expectedBytes);
    }

    public static IEnumerable<(
        string value,
        long expectedBytes,
        bool expectedCanParse
    )> DataSource()
    {
        yield return ("512", 512, true);
        yield return ("1KB", 1_000, true);
        yield return ("1KiB", 1_024, true);
        yield return ("1.5MB", 1_500_000, true);
        yield return ("5GB", 5_000_000_000, true);
        yield return ("invalid", 0, false);
        yield return ("1XB", 0, false);
    }
}
