using Cocona.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Watch.Cli.Configuration;

internal static class ConfigurationExtensions
{
    public static void AddConfiguration(this CoconaAppBuilder coconaAppBuilder)
    {
        coconaAppBuilder.Configuration.Sources.Clear();

        if (coconaAppBuilder.Environment.IsDevelopment())
        {
            coconaAppBuilder.Configuration.AddUserSecrets<Program>();
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.Development.json");
        }
        else
        {
            coconaAppBuilder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.json");
        }
    }
}