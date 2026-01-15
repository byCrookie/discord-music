using System.Text;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CacheOptions>, ValidateSettingsOptions>()
        );

        builder.Services.AddSingleton<IMusicCache, MusicCache>();
        return builder;
    }

    private sealed class ValidateSettingsOptions : IValidateOptions<CacheOptions>
    {
        public ValidateOptionsResult Validate(string? name, CacheOptions options)
        {
            StringBuilder? failure = null;

            if (!ByteSize.TryParse(options.MaxSize, out _))
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(CacheOptions.MaxSize)} {options.MaxSize} is not a valid hex color"
                );
            }

            return failure is not null
                ? ValidateOptionsResult.Fail(failure.ToString())
                : ValidateOptionsResult.Success;
        }
    }
}
