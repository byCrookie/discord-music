using System.IO.Abstractions;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Queue;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Core;

public static class CoreModule
{
    public static IHostApplicationBuilder AddCore(this IHostApplicationBuilder builder, CancellationToken ct)
    {
        builder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");

        builder.AddUtils();
        builder.AddYouTube();
        builder.AddSpotify();
        builder.AddLyrics();
        builder.AddCache();
        builder.AddQueue();
        builder.AddDiscord();
        builder.AddAudio();

        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());

        builder.Services.AddSingleton(new Cancellation(ct));

        return builder;
    }

    public static IHost UseCore(this IHost host)
    {
        host.UseDiscord();
        return host;
    }
}
