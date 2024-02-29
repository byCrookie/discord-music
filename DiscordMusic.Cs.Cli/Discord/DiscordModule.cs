using DiscordMusic.Cs.Cli.Discord.Options;

namespace DiscordMusic.Cs.Cli.Discord;

internal static class DiscordModule
{
    public static void AddDiscord(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IState, State>();

        services.AddDiscordOptions(configuration);
    }
}
