using System.CommandLine;

namespace DiscordMusic.Client.Spotify;

public static class SpotifyCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("spotify", "Spotify commands") { IsHidden = true };

        command.AddCommand(SpotifySearchCommand.Create(args));
        return command;
    }
}
