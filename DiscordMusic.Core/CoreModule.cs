using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core;

public static class CoreModule
{
    public static void AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDiscord(configuration);
        services.AddUtils();
    }
}