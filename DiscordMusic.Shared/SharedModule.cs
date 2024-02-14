using DiscordMusic.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Shared;

public static class SharedModule
{
    public static void AddShared(this IServiceCollection services)
    {
        services.AddUtils();
    }
}
