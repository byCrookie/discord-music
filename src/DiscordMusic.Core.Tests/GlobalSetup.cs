using System.Diagnostics.CodeAnalysis;

[assembly: Retry(3)]
[assembly: ExcludeFromCodeCoverage]

namespace DiscordMusic.Core.Tests;

public class GlobalHooks
{
    [Before(TestSession)]
    public static void SetUp() { }

    [After(TestSession)]
    public static void CleanUp() { }
}
