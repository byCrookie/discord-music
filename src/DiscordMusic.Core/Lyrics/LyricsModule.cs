using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Lyrics;

public static class LyricsModule
{
    public static IHostApplicationBuilder AddLyrics(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<LyricsOptions>()
            .Bind(builder.Configuration.GetSection(LyricsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        builder.Services.AddTransient<ILyricsSearch, LyricsSearch>();
        return builder;
    }
}