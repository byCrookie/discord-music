using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using DiscordMusic.Core.Storage;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Client.Storage;

public sealed class StorageClearCommand : Command
{
    public StorageClearCommand(string[] args)
        : base("clear", "Clear storage")
    {
        Action = new StorageClearCommandAction(args);
    }

    private class StorageClearCommandAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            using var host = StorageCommandHost.Build(args);
            var fileSystem = host.Services.GetRequiredService<IFileSystem>();
            var storagePathProvider = host.Services.GetRequiredService<IStoragePathProvider>();
            var trimmer = host.Services.GetRequiredService<IStorageCacheTrimmer>();
            var options = host.Services.GetRequiredService<IOptions<StorageOptions>>();

            if (
                await StorageCommandHost.TryWriteMaxSizeErrorAsync(
                    parseResult,
                    options.Value.MaxSize
                )
            )
            {
                return 1;
            }

            var storagePath = storagePathProvider.StorageDirectory().FullName;
            var beforeBytes = StorageCommandHost.CacheSizeBytes(fileSystem, storagePath);
            await trimmer.TrimAsync(storagePath, maxBytes: 0, cancellationToken);
            var afterBytes = StorageCommandHost.CacheSizeBytes(fileSystem, storagePath);
            var freedBytes = Math.Max(0, beforeBytes - afterBytes);

            await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                $"Cleared storage cache. Freed {ByteSize.FromBytes(freedBytes).Humanize()}; remaining cache size is {ByteSize.FromBytes(afterBytes).Humanize()} ({storagePath})."
            );
            return 0;
        }
    }
}
