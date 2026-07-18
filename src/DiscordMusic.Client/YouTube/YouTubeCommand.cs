using System.CommandLine;

namespace DiscordMusic.Client.YouTube;

public sealed class YouTubeCommand : Command
{
    public YouTubeCommand(string[] args)
        : base("youtube", "YouTube commands")
    {
        Add(new YouTubeSearchCommand(args));
        Add(new YouTubeDownloadCommand(args));

        Hidden = true;
    }
}
