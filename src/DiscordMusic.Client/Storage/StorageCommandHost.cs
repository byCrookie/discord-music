using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Observability;
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
    public static IHost Build(string[] args, string dotEnvPath)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        var fileSystem = new RealFileSystem();
        var environmentVariables = SystemEnvironmentVariables.Instance;
        builder.Services.AddSingleton<IFileSystem>(fileSystem);
        builder.Services.AddSingleton<IEnvironmentVariables>(environmentVariables);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Configuration.AddDiscordMusicEnvironment(
            builder.Environment,
            NullLogger.Instance,
            fileSystem,
            environmentVariables,
            dotEnvPath
        );
        builder.AddUtils();
        builder.AddStorage();
        return builder.Build();
    }

    public static long CacheSizeBytes(IFileSystem fileSystem, string storagePath)
    {
        using var activity = DiscordMusicObservability.StartActivity(
            "storage.cache.size.calculate"
        );
        DiscordMusicObservability.SetTag(activity, "storage.path.length", storagePath.Length);
        var result = "completed";

        try
        {
            if (!fileSystem.Directory.Exists(storagePath))
            {
                result = "missing_directory";
                activity?.SetStatus(ActivityStatusCode.Ok, result);
                return 0;
            }

            var bytes = CacheFiles(fileSystem, storagePath).Sum(file => file.Length);
            DiscordMusicObservability.SetTag(activity, "storage.cache.size", bytes);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return bytes;
        }
        catch (Exception ex)
        {
            result = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            DiscordMusicObservability.StorageOperations.Add(
                1,
                new KeyValuePair<string, object?>("operation", "cache_size_calculate"),
                new KeyValuePair<string, object?>("result", result)
            );
        }
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
