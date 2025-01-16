using System.CommandLine;

namespace DiscordMusic.Client.Spotify;

public static class SpotifyCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("spotify", "Spotify commands");
        command.AddCommand(SpotifySearchCommand.Create(args));
        return command;
    }
}
