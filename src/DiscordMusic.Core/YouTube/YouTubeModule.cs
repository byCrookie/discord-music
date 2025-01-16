using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.YouTube;

public static class YouTubeModule
{
    public static IHostApplicationBuilder AddYouTube(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<YouTubeOptions>()
            .Bind(builder.Configuration.GetSection(YouTubeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddTransient<IYoutubeSearch, YoutubeSearch>();
        builder.Services.AddTransient<IYouTubeDownload, YouTubeDownload>();

        return builder;
    }
}