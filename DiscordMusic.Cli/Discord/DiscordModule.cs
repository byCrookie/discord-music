using DiscordMusic.Cli.Discord.Music;
using DiscordMusic.Cli.Discord.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Cli.Discord;

internal static class DiscordModule
{
    public static void AddDiscord(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDiscordOptions(configuration);
        services.AddMusic();
    }
}
