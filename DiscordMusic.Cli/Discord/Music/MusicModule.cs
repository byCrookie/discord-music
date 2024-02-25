using DiscordMusic.Cli.Discord.Music.Download;
using DiscordMusic.Cli.Discord.Music.Lyrics;
using DiscordMusic.Cli.Discord.Music.Queue;
using DiscordMusic.Cli.Discord.Music.Spotify;
using DiscordMusic.Cli.Discord.Music.Store;
using DiscordMusic.Cli.Discord.Music.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Cli.Discord.Music;

internal static class MusicModule
{
    public static void AddMusic(this IServiceCollection services)
    {
        services.AddSingleton<IMusicQueue, MusicQueue>();
        services.AddSingleton<IMusicStore, MusicStore>();
        services.AddSingleton<IMusicStreamer, MusicStreamer>();
        services.AddTransient<IMusicDownloader, MusicDownloader>();
        services.AddTransient<ILyricsService, LyricsService>();
        services.AddTransient<ISpotify, Spotify.Spotify>();

        services.AddHostedService<MusicHost>();
    }
}
