using System.CommandLine;
using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Storage;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testably.Abstractions;

namespace DiscordMusic.Client.Storage;

internal static class StorageCommandHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        var fileSystem = new RealFileSystem();
        var environmentVariables = SystemEnvironmentVariables.Instance;
        builder.Services.AddSingleton<IFileSystem>(fileSystem);
        builder.Services.AddSingleton<IEnvironmentVariables>(environmentVariables);
        builder.Configuration.AddDiscordMusicEnvironment(
            builder.Environment,
            NullLogger.Instance,
            fileSystem,
            environmentVariables,
            ".env"
        );
        builder.AddUtils();
        builder.AddStorage();
        return builder.Build();
    }

    public static long CacheSizeBytes(IFileSystem fileSystem, string storagePath)
    {
        if (!fileSystem.Directory.Exists(storagePath))
        {
            return 0;
        }

        return CacheFiles(fileSystem, storagePath).Sum(file => file.Length);
    }

    public static IEnumerable<IFileInfo> CacheFiles(IFileSystem fileSystem, string storagePath)
    {
        if (!fileSystem.Directory.Exists(storagePath))
        {
            return [];
        }

        return fileSystem
            .DirectoryInfo.New(storagePath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => !file.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            .Where(file => file.Exists);
    }

    public static async Task<bool> TryWriteMaxSizeErrorAsync(
        ParseResult parseResult,
        string maxSize
    )
    {
        if (StorageSizeParser.TryParseBytes(maxSize, out _))
        {
            return false;
        }

        await parseResult.InvocationConfiguration.Error.WriteLineAsync(
            $"Invalid storage max size `{maxSize}`."
        );
        return true;
    }
}
