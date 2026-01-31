using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Discord.Cache;

public static class CacheModule
{
    public static IHostApplicationBuilder AddCache(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMusicCache, MusicCache>();
        return builder;
    }
}
