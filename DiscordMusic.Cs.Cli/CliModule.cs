using Discord.Rest;
using DiscordMusic.Core;
using DiscordMusic.Cs.Cli.Discord;

namespace DiscordMusic.Cs.Cli;

internal static class CliModule
{
    public static void AddCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCore();
        services.AddSingleton<DiscordRestClient>();
        services.AddDiscord(configuration);
    }
}
