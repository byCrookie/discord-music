using System.Reflection;
using Cocona.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Configuration;

public static class ConfigurationExtensions
{
    public static void AddConfiguration(this CoconaAppBuilder coconaAppBuilder, Assembly secretsAssembly)
    {
        coconaAppBuilder.Configuration.Sources.Clear();

        if (coconaAppBuilder.Environment.IsDevelopment())
        {
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.Development.json", true);
            coconaAppBuilder.Configuration.AddUserSecrets(secretsAssembly);
        }
        else
        {
            coconaAppBuilder.Configuration.AddJsonFile("appsettings.json", true);
            coconaAppBuilder.Configuration.AddEnvironmentVariables("DISCORD_MUSIC_");
        }
    }
}
