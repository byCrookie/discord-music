using System.CommandLine;

namespace DiscordMusic.Client.YouTube;

public static class YouTubeCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("youtube", "YouTube commands") { IsHidden = true };

        command.AddCommand(YouTubeSearchCommand.Create(args));
        command.AddCommand(YouTubeDownloadCommand.Create(args));
        return command;
    }
}
