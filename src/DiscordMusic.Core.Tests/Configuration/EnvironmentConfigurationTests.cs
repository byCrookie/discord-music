using DiscordMusic.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions.Testing;

namespace DiscordMusic.Core.Tests.Configuration;

public class EnvironmentConfigurationTests
{
    [Test]
    [MethodDataSource(typeof(FileSystemTestData), nameof(FileSystemTestData.SimulationModes))]
    public async Task PrefixedValuesHaveHighestPriority(SimulationMode mode)
    {
        var fileSystem = FileSystemTestData.CreateFileSystem(mode);
        var appDirectory = fileSystem.DirectoryInfo.New("/app").FullName;
        var dotEnvPath = fileSystem.Path.Combine(appDirectory, ".env");
        fileSystem.Directory.CreateDirectory(appDirectory);
        await fileSystem.File.WriteAllTextAsync(
            dotEnvPath,
            """
            CONFIG_TEST__DOTENV_BEATS_UNPREFIXED_ENV=from-unprefixed-dotenv
            DISCORD_MUSIC_CONFIG_TEST__DOTENV_BEATS_UNPREFIXED_ENV=from-prefixed-dotenv
            DISCORD_MUSIC_CONFIG_TEST__PREFIXED_ENV_BEATS_DOTENV=from-prefixed-dotenv
            """
        );
        var environmentVariables = new TestEnvironmentVariables(
            new Dictionary<string, string?>
            {
                ["CONFIG_TEST__DOTENV_BEATS_UNPREFIXED_ENV"] = "from-unprefixed-env",
                ["DISCORD_MUSIC_CONFIG_TEST__PREFIXED_ENV_BEATS_DOTENV"] = "from-prefixed-env",
            }
        );

        var configuration = new ConfigurationBuilder()
            .AddDiscordMusicEnvironment(
                new TestHostEnvironment(),
                NullLogger.Instance,
                fileSystem: fileSystem,
                environmentVariables: environmentVariables,
                dotEnvPath: dotEnvPath
            )
            .Build();

        await Assert
            .That(configuration["config_test:dotenv_beats_unprefixed_env"])
            .IsEqualTo("from-prefixed-dotenv");
        await Assert
            .That(configuration["config_test:prefixed_env_beats_dotenv"])
            .IsEqualTo("from-prefixed-env");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(EnvironmentConfigurationTests);
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
