using DiscordMusic.Core.Discord.Gateway.Client;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.Gateway;

internal static class GatewayModule
{
   public static void AddGateway(this IServiceCollection services)
   {
      services.AddTransient<IGatewayService, GatewayService>();
      services.AddGatewayClient();
   }
}