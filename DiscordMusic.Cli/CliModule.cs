using DiscordMusic.Cli.Data;
using DiscordMusic.Cli.Discord;
using DiscordMusic.Cli.Environment;
using DiscordMusic.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Cli;

internal static class CliModule
{
    public static void AddCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCore();
        services.AddDiscord(configuration);
        services.AddEnvironment();
        services.AddData();
    }
}
