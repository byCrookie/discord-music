using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.YouTube;

public static class YouTubeModule
{
    public static IHostApplicationBuilder AddYouTube(this IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient<IYoutubeSearch, YoutubeSearch>();
        builder.Services.AddTransient<IYouTubeDownload, YouTubeDownload>();

        return builder;
    }
}
