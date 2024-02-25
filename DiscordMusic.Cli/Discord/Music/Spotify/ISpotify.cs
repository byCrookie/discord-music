namespace DiscordMusic.Cli.Discord.Music.Spotify;

public interface ISpotify
{
    Task<List<Track>> GetTracksAsync(string argument);
}
