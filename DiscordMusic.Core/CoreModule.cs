using DiscordMusic.Core.Data;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Environment;
using DiscordMusic.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core;

public static class CoreModule
{
    public static void AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDiscord(configuration);
        services.AddEnvironment();
        services.AddData();
        services.AddShared();
    }
}