using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Discord.Cache;

public static class CacheModule
{
    public static IHostApplicationBuilder AddCache(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<CacheOptions>()
            .Bind(builder.Configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IMusicCache, MusicCache>();
        return builder;
    }
}
