using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Audio;

public static class AudioModule
{
    public static IHostApplicationBuilder AddAudio(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AudioPlayer>();

        return builder;
    }
}
