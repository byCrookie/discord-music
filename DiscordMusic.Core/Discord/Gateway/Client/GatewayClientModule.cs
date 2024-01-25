using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord.Gateway.Client;

internal static class GatewayClientModule
{
   public static void AddGatewayClient(this IServiceCollection services)
   {
      services.AddTransient<IGatewayClient, GatewayClient>();
   }
}