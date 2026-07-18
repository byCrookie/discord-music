using System.Collections;
using System.IO.Abstractions;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Configuration;

public static class EnvironmentConfiguration
{
    private const string Prefix = "DISCORD_MUSIC_";

    extension(IConfigurationBuilder configuration)
    {
        public IConfigurationBuilder AddDiscordMusicEnvironment(
            IHostEnvironment environment,
            ILogger logger,
            IFileSystem fileSystem,
            IEnvironmentVariables environmentVariables,
            string dotEnvPath
        )
        {
            var dotEnvFullPath = fileSystem.Path.GetFullPath(dotEnvPath);
            logger.LogDebug(
                "Adding Discord Music configuration. Environment={EnvironmentName} Application={ApplicationName} DotEnvPath={DotEnvPath} DotEnvFullPath={DotEnvFullPath}",
                environment.EnvironmentName,
                environment.ApplicationName,
                dotEnvPath,
                dotEnvFullPath
            );

            var dotEnvExists = fileSystem.File.Exists(dotEnvFullPath);
            var dotEnv = LoadDotEnv(fileSystem, dotEnvFullPath);
            if (dotEnvExists)
            {
                logger.LogInformation(
                    "Loaded Discord Music .env configuration from {DotEnvPath}. UnprefixedEntries={UnprefixedEntries} PrefixedEntries={PrefixedEntries}",
                    dotEnvFullPath,
                    dotEnv.Unprefixed.Count,
                    dotEnv.Prefixed.Count
                );
            }
            else
            {
                logger.LogInformation(
                    "Discord Music .env file {DotEnvPath} was not found; continuing with environment variables and user secrets.",
                    dotEnvFullPath
                );
            }

            var environmentValues = environmentVariables.GetVariables();
            var env = SplitByPrefix(environmentValues);
            logger.LogInformation(
                "Loaded Discord Music environment configuration. TotalVariables={TotalVariables} UnprefixedEntries={UnprefixedEntries} PrefixedEntries={PrefixedEntries}",
                environmentValues.Count,
                env.Unprefixed.Count,
                env.Prefixed.Count
            );

            configuration.AddInMemoryCollection(dotEnv.Unprefixed);
            configuration.AddInMemoryCollection(env.Unprefixed);
            configuration.AddInMemoryCollection(dotEnv.Prefixed);
            configuration.AddInMemoryCollection(env.Prefixed);
            configuration.AddDevelopmentUserSecrets(environment, logger);
            return configuration;
        }

        private void AddDevelopmentUserSecrets(IHostEnvironment environment, ILogger logger)
        {
            if (environment.ApplicationName.Length == 0 || !environment.IsDevelopment())
            {
                logger.LogDebug(
                    "Skipping development user secrets. Environment={EnvironmentName} Application={ApplicationName}",
                    environment.EnvironmentName,
                    environment.ApplicationName
                );
                return;
            }

            try
            {
                var appAssembly = Assembly.Load(new AssemblyName(environment.ApplicationName));
                configuration.AddUserSecrets(appAssembly, true, false);
                logger.LogDebug(
                    "Added development user secrets for application {ApplicationName}.",
                    environment.ApplicationName
                );
            }
            catch (FileNotFoundException)
            {
                logger.LogWarning(
                    "User secrets assembly '{AssemblyName}' not found.",
                    environment.ApplicationName
                );
            }
        }
    }

    private static DotEnvValues LoadDotEnv(IFileSystem fileSystem, string path)
    {
        if (!fileSystem.File.Exists(path))
        {
            return new DotEnvValues([], []);
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in fileSystem.File.ReadLines(path))
        {
            if (TryParseLine(line, out var key, out var value))
            {
                values[key] = value;
            }
        }

        return SplitByPrefix(values);
    }

    private static DotEnvValues SplitByPrefix(IReadOnlyDictionary<string, string?> values)
    {
        var unprefixed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var prefixed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                prefixed[NormalizeKey(key[Prefix.Length..])] = value;
            }
            else
            {
                unprefixed[NormalizeKey(key)] = value;
            }
        }

        return new DotEnvValues(unprefixed, prefixed);
    }

    private static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        const string export = "export ";
        if (trimmed.StartsWith(export, StringComparison.Ordinal))
        {
            trimmed = trimmed[export.Length..].TrimStart();
        }

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = trimmed[..separator].Trim();
        value = Unquote(trimmed[(separator + 1)..].Trim());
        return key.Length > 0;
    }

    private static string NormalizeKey(string key)
    {
        return key.Replace("__", ConfigurationPath.KeyDelimiter);
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var quote = value[0];
        if (quote is '"' or '\'' && value[^1] == quote)
        {
            return value[1..^1];
        }

        return value;
    }

    private sealed record DotEnvValues(
        Dictionary<string, string?> Unprefixed,
        Dictionary<string, string?> Prefixed
    );
}

public interface IEnvironmentVariables
{
    IReadOnlyDictionary<string, string?> GetVariables();

    string? GetVariable(string variable);

    string GetFolderPath(Environment.SpecialFolder folder);
}

public sealed class SystemEnvironmentVariables : IEnvironmentVariables
{
    public static SystemEnvironmentVariables Instance { get; } = new();

    private SystemEnvironmentVariables() { }

    public IReadOnlyDictionary<string, string?> GetVariables()
    {
        var variables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            if (item.Key is string key)
            {
                variables[key] = item.Value?.ToString();
            }
        }

        return variables;
    }

    public string? GetVariable(string variable)
    {
        return Environment.GetEnvironmentVariable(variable);
    }

    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return Environment.GetFolderPath(folder);
    }
}
