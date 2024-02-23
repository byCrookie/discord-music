namespace DiscordMusic.Core.Discord.Music.Spotify;

public interface ISpotify
{
    Task<List<Track>> GetTracksAsync(string argument);
}
