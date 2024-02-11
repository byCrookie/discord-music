using DiscordMusic.Watch.Cli.Discord.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Watch.Cli.Discord;

internal static class DiscordModule
{
   public static void AddDiscord(this IServiceCollection services, IConfiguration configuration)
   {
      services.AddDiscordOptions(configuration);
   }
}