using System.Text;
using DiscordMusic.Core.Discord.Cache;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Config;

public static class ConfigModule
{
    public static void AddConfig(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<ConfigOptions>()
            .Bind(builder.Configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<ConfigOptions>,
                ValidateSettingsConfigOptions
            >()
        );

        builder
            .Services.AddOptions<CacheOptions>()
            .Bind(builder.Configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<CacheOptions>,
                ValidateSettingsCacheOptions
            >()
        );

        builder.Services.AddTransient<AppPaths>();
    }

    private sealed class ValidateSettingsConfigOptions : IValidateOptions<ConfigOptions>
    {
        public ValidateOptionsResult Validate(string? name, ConfigOptions options)
        {
            List<string>? errors = null;

            if (
                !string.IsNullOrWhiteSpace(options.ConfigFile)
                && !File.Exists(Path.GetFullPath(options.ConfigFile))
            )
            {
                (errors ??= []).Add(
                    $"{ConfigOptions.ConfigFileKey} '{Path.GetFullPath(options.ConfigFile)}' does not exist."
                );
            }

            return errors is not null
                ? ValidateOptionsResult.Fail(errors)
                : ValidateOptionsResult.Success;
        }
    }

    private sealed class ValidateSettingsCacheOptions : IValidateOptions<CacheOptions>
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
