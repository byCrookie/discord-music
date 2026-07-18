using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.YouTube;

public static class YouTubeClientModule
{
    public static void AddYouTubeClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedService<YouTubeSearchRequestConsumerService>();
        builder.Services.AddHostedService<YouTubeDownloadRequestConsumerService>();
    }
}
