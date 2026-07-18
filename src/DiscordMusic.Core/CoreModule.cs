using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testably.Abstractions;

namespace DiscordMusic.Core;

public static class CoreModule
{
    public static void AddCore(
        this IHostApplicationBuilder builder,
        ILogger logger,
        string dotEnvPath,
        CancellationToken ct
    )
    {
        var fileSystem = new RealFileSystem();
        var environmentVariables = SystemEnvironmentVariables.Instance;
        builder.Services.AddSingleton<IFileSystem>(fileSystem);
        builder.Services.AddSingleton<IEnvironmentVariables>(environmentVariables);

        builder.Configuration.AddDiscordMusicEnvironment(
            builder.Environment,
            logger,
            fileSystem,
            environmentVariables,
            dotEnvPath
        );

        builder.AddUtils();
        builder.AddYouTube();
        builder.AddSpotify();
        builder.AddLyrics();
        builder.AddDiscord();
        builder.AddStorage();

        builder.Services.AddSingleton(new Cancellation(ct));
    }

    public static void UseCore(this IHost host)
    {
        host.UseDiscord();
    }
}
