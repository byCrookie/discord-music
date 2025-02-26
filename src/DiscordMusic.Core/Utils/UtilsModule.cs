using DiscordMusic.Core.Utils.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Utils;

public static class UtilsModule
{
    public static IHostApplicationBuilder AddUtils(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IJsonSerializer>(new JsonSerializer());
        return builder;
    }
}
