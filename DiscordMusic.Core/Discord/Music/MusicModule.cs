using DiscordMusic.Core.Discord.Music.Download;
using DiscordMusic.Core.Discord.Music.Queue;
using DiscordMusic.Core.Discord.Music.Store;
using DiscordMusic.Core.Discord.Music.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.Music;

internal static class MusicModule
{
    public static void AddMusic(this IServiceCollection services)
    {
        services.AddSingleton<IMusicQueue, MusicQueue>();
        services.AddSingleton<IMusicStore, MusicStore>();
        services.AddSingleton<IMusicStreamer, MusicStreamer>();
        services.AddTransient<IMusicDownloader, MusicDownloader>();

        services.AddHostedService<MusicHost>();
    }
}
