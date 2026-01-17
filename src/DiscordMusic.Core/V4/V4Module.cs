using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.V4;

public static class V4Module
{
    public static IHostApplicationBuilder AddQueue(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(typeof(DiskCache<>));
        return builder;
    }
}
