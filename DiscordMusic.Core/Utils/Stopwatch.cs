namespace DiscordMusic.Core.Utils;

internal class Stopwatch : IStopwatch
{
    public System.Diagnostics.Stopwatch StartNew()
    {
        return System.Diagnostics.Stopwatch.StartNew();
    }
}