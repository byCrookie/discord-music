using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Cs.Cli.Discord.Options;

internal static class OptionsModule
{
    public static void AddDiscordOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordCsOptions>()
            .Bind(configuration.GetSection(DiscordCsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}