using System.IO.Abstractions;
using Discord.Rest;
using DiscordMusic.Cs.Cli.Discord;
using Serilog;

namespace DiscordMusic.Cs.Cli;

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