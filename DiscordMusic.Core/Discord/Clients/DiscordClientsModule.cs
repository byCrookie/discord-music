using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.Clients;

internal static class DiscordClientsModule
{
    public static void AddClients(this IServiceCollection services)
    {
        services.AddSingleton<IDiscordBotClient, DiscordBotClient>();

        services.AddMemoryCache();
    }
}