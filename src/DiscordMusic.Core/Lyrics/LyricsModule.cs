using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Lyrics;

public static class LyricsModule
{
    public static void AddLyrics(this IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient<ILyricsSearch, LyricsSearch>();
    }
}
