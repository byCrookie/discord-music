using DiscordMusic.Core.Utils.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Utils;

internal static class UtilsModule
{
    public static void AddUtils(this IServiceCollection services)
    {
        services.AddTransient<IJsonSerializer, JsonSerializer>();
    }
}