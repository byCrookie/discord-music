using System.Reflection;
using Cocona.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Shared.Configuration;

public static class ConfigurationExtensions
{
    public static void AddConfiguration(this CoconaAppBuilder coconaAppBuilder, Assembly secretsAssembly)
    {
        coconaAppBuilder.Configuration.Sources.Clear();

        if (coconaAppBuilder.Environment.IsDevelopment())
        {
            coconaAppBuilder.Configuration.AddUserSecrets(secretsAssembly);
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.Development.json");
        }
        else
        {
            coconaAppBuilder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.json");
        }
    }
}
