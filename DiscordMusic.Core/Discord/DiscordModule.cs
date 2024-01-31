using DiscordMusic.Core.Discord.Music;
using DiscordMusic.Core.Discord.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Core.Discord;

internal static class DiscordModule
{
   public static void AddDiscord(this IServiceCollection services, IConfiguration configuration)
   {
      services.AddDiscordOptions(configuration);
      services.AddMusic();
   }
}