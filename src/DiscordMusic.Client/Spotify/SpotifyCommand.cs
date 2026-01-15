using System.CommandLine;

namespace DiscordMusic.Client.Spotify;

public static class SpotifyCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("spotify", "Spotify commands") { Hidden = true };
        command.Add(SpotifySearchCommand.Create(args));
        return command;
    }
}
