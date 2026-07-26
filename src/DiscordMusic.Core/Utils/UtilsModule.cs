using System.IO.Abstractions;
using DiscordMusic.Core.Utils.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Utils;

public static class UtilsModule
{
    public static void AddUtils(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IJsonSerializer>(new JsonSerializer());
        builder.Services.AddTransient(implementationFactory: provider => new BinaryLocator(
            provider.GetRequiredService<IFileSystem>(),
            provider.GetRequiredService<ILogger<BinaryLocator>>(),
            AppContext.BaseDirectory
        ));
        builder.Services.AddSingleton<ICliCommandRunner, CliWrapCommandRunner>();
    }
}
