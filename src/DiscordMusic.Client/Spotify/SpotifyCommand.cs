using System.CommandLine;

namespace DiscordMusic.Client.Spotify;

public sealed class SpotifyCommand : Command
{
    public SpotifyCommand(string[] args)
        : base("spotify", "Spotify commands")
    {
        Add(new SpotifySearchCommand(args));

        Hidden = true;
    }
}
