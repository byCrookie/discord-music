using ErrorOr;

namespace DiscordMusic.Core.Lyrics;

public interface ILyricsSearch
{
    Task<ErrorOr<Lyrics>> SearchAsync(string title, string artist, CancellationToken ct);
}
