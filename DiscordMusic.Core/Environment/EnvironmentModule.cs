using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Environment;

internal static class EnvironmentModule
{
    public static void AddEnvironment(this IServiceCollection services)
    {
        services.AddSingleton<IEnvironment, Environment>();
    }
}
