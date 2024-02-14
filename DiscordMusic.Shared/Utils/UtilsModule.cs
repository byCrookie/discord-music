using DiscordMusic.Shared.Utils.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Shared.Utils;

internal static class UtilsModule
{
    public static void AddUtils(this IServiceCollection services)
    {
        services.AddTransient<IJsonSerializer, JsonSerializer>();
    }
}