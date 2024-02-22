namespace DiscordMusic.Core.Discord.Music.Lyrics;

public interface ILyricsService
{
    Task<Lyrics?> GetLyricsAsync(string title, string author);
}
