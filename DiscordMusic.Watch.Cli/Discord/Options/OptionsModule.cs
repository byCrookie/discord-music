using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Watch.Cli.Discord.Options;

internal static class OptionsModule
{
    public static void AddDiscordOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordWatchOptions>()
            .Bind(configuration.GetSection(DiscordWatchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}