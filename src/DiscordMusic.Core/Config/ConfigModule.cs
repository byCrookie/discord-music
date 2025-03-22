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
            ServiceDescriptor.Singleton<IValidateOptions<ConfigOptions>, ValidateSettingsOptions>()
        );
    }

    private sealed class ValidateSettingsOptions : IValidateOptions<ConfigOptions>
    {
        public ValidateOptionsResult Validate(string? name, ConfigOptions options)
        {
            List<string>? errors = null;

            if (!string.IsNullOrWhiteSpace(options.ConfigFile) && !File.Exists(Path.GetFullPath(options.ConfigFile)))
            {
                (errors ??= []).Add(
                    $"{ConfigOptions.ConfigFileKey} '{Path.GetFullPath(options.ConfigFile)}' does not exist."
                );
            }

            return errors is not null ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
        }
    }
}
