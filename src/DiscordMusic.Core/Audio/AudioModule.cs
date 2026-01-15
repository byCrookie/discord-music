using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Audio;

public static class AudioModule
{
    public static IHostApplicationBuilder AddAudio(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<AudioOptions>()
            .Bind(builder.Configuration.GetSection(AudioOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AudioOptions>, ValidateSettingsOptions>()
        );

        builder.Services.AddSingleton<IAudioPlayer, AudioPlayer>();

        return builder;
    }

    private sealed class ValidateSettingsOptions : IValidateOptions<AudioOptions>
    {
        public ValidateOptionsResult Validate(string? name, AudioOptions options)
        {
            StringBuilder? failure = null;

            if (!TimeSpan.TryParse(options.Buffer, out _))
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(AudioOptions.Buffer)} {options.Buffer} is not a valid timespan"
                );
            }

            return failure is not null
                ? ValidateOptionsResult.Fail(failure.ToString())
                : ValidateOptionsResult.Success;
        }
    }
}
