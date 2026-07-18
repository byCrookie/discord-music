using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests;

public static class FileSystemTestData
{
    public static IEnumerable<SimulationMode> SimulationModes()
    {
        yield return SimulationMode.Linux;
        yield return SimulationMode.Windows;
        yield return SimulationMode.MacOS;
    }

    public static MockFileSystem CreateFileSystem(SimulationMode mode)
    {
        return new MockFileSystem(options => options.SimulatingOperatingSystem(mode));
    }
}
