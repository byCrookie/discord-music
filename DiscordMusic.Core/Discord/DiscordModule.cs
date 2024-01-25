using DiscordMusic.Core.Discord.Clients;
using DiscordMusic.Core.Discord.Gateway;
using DiscordMusic.Core.Discord.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord;

internal static class DiscordModule
{
   public static void AddDiscord(this IServiceCollection services, IConfiguration configuration)
   {
      services.AddGateway();
      services.AddDiscordOptions(configuration);
      services.AddClients();
   }
}