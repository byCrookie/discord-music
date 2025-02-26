using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpotifyAPI.Web;

namespace DiscordMusic.Core.Spotify;

public static class SpotifyModule
{
    public static IHostApplicationBuilder AddSpotify(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<SpotifyOptions>()
            .Bind(builder.Configuration.GetSection(SpotifyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton(SpotifyClientConfig.CreateDefault());
        builder.Services.AddTransient<ISpotifySeacher, SpotifySeacher>();
        return builder;
    }
}
