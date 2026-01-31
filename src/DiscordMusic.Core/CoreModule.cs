using System.IO.Abstractions;
using System.Reflection;
using DiscordMusic.Core.Config;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Discord.VoiceCommands;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testably.Abstractions;

namespace DiscordMusic.Core;

public static class CoreModule
{
    private const string Dmrc = ".dmrc";

    public static void AddCore(
        this IHostApplicationBuilder builder,
        ILogger logger,
        CancellationToken ct
    )
    {
        builder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");

        builder.AddConfig();

        AddConfigFromOsSpecificDirs(builder, logger);
        AddConfigurationFromExecution(builder);
        AddConfigurationFromEnvOrDmrc(builder);

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
        builder.AddDiscord();

        builder.Services.AddVoiceCommands();

        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.Services.AddSingleton(new Cancellation(ct));

        builder.Services.AddSingleton<VoiceCommandManager>();
        builder.Services.AddSingleton<IVoiceCommandSubscriptions, VoiceCommandSubscriptions>();
        builder.Services.AddHostedService<VoiceCommandService>();
    }

    private static void AddConfigFromOsSpecificDirs(IHostApplicationBuilder builder, ILogger logger)
    {
        var configDir = AppPaths.Config(logger);
        if (File.Exists(Path.Combine(configDir, Dmrc)))
        {
            builder.Configuration.AddIniFile(
                new PhysicalFileProvider(configDir, ExclusionFilters.None),
                Dmrc,
                reloadOnChange: false,
                optional: false
            );
        }
    }

    private static void AddConfigurationFromExecution(IHostApplicationBuilder builder)
    {
        var executionDir =
            Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
            ?? Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(executionDir, Dmrc)))
        {
            builder.Configuration.AddIniFile(
                new PhysicalFileProvider(executionDir, ExclusionFilters.None),
                Dmrc,
                reloadOnChange: false,
                optional: false
            );
        }
    }

    private static void AddConfigurationFromEnvOrDmrc(IHostApplicationBuilder builder)
    {
        var config = builder.Configuration.GetValue<string?>(ConfigOptions.ConfigFileKey);

        if (string.IsNullOrWhiteSpace(config))
        {
            return;
        }

        if (!File.Exists(config))
        {
            throw new FileNotFoundException(
                $"Configuration file '{Path.GetFullPath(config)}' does not exist."
            );
        }

        builder.Configuration.AddIniFile(config, false, false);
    }

    public static IHost UseCore(this IHost host)
    {
        host.UseDiscord();
        return host;
    }
}
