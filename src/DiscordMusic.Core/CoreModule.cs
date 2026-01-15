using System.IO.Abstractions;
using System.Reflection;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Config;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Queue;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Core;

public static class CoreModule
{
    private const string Dmrc = ".dmrc";

    public static void AddCore(this IHostApplicationBuilder builder, CancellationToken ct)
    {
        builder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");

        builder.AddConfig();

        var config = builder.Configuration.GetValue<string?>(ConfigOptions.ConfigFileKey);

        if (!string.IsNullOrWhiteSpace(config))
        {
            if (!File.Exists(config))
            {
                throw new FileNotFoundException(
                    $"Configuration file '{Path.GetFullPath(config)}' does not exist."
                );
            }

            builder.Configuration.AddJsonFile(config, false, false);
        }

        builder.Configuration.AddIniFile(
            new PhysicalFileProvider(EvalDmrcPath(), ExclusionFilters.None),
            Dmrc,
            reloadOnChange: false,
            optional: true
        );

        builder.Configuration.AddIniFile(
            new PhysicalFileProvider(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                    ?? Directory.GetCurrentDirectory(),
                ExclusionFilters.None
            ),
            Dmrc,
            reloadOnChange: false,
            optional: true
        );

        if (
            builder.Environment.IsDevelopment()
            && builder.Environment.ApplicationName is { Length: > 0 }
        )
        {
            try
            {
                var appAssembly = Assembly.Load(
                    new AssemblyName(builder.Environment.ApplicationName)
                );
                builder.Configuration.AddUserSecrets(appAssembly, true, false);
            }
            catch (FileNotFoundException)
            {
                // The assembly cannot be found, so just skip it.
            }
        }

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
    }

    public static IHost UseCore(this IHost host)
    {
        host.UseDiscord();
        return host;
    }

    private static string EvalDmrcPath()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
