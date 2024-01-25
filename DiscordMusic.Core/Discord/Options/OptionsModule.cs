using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.Options;

internal static class OptionsModule
{
    public static void AddDiscordOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordSecrets>()
            .Bind(configuration.GetSection("Discord"))
            .ValidateDataAnnotations();
    }
}