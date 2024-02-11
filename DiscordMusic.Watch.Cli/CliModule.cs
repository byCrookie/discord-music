using System.IO.Abstractions;
using Discord.Rest;
using DiscordMusic.Watch.Cli.Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DiscordMusic.Watch.Cli;

internal static class CliModule
{
    public static void AddCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog();
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddSingleton<DiscordRestClient>();
        services.AddDiscord(configuration);
    }
}