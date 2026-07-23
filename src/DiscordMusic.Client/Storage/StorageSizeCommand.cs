using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using DiscordMusic.Client.Music;
using DiscordMusic.Core.Configuration;
using DiscordMusic.Core.Storage;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Client.Storage;

public sealed class StorageSizeCommand : Command
{
    public StorageSizeCommand(string[] args)
        : base("size", "Get the size of data in storage")
    {
        Action = new StorageSizeCommandAction(args);
    }

    private class StorageSizeCommandAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            var dotEnvPath = EnvironmentConfiguration.ResolveDotEnvPath(
                parseResult.GetValue(DiscordMusicCommand.EnvFileOption),
                SystemEnvironmentVariables.Instance
            );
            using var host = StorageCommandHost.Build(args, dotEnvPath);
            var fileSystem = host.Services.GetRequiredService<IFileSystem>();
            var storagePathProvider = host.Services.GetRequiredService<IStoragePathProvider>();
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
            var bytes = StorageCommandHost.CacheSizeBytes(fileSystem, storagePath);
            StorageSizeParser.TryParseBytes(options.Value.MaxSize, out var maxBytes);

            await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                $"Storage cache: {ByteSize.FromBytes(bytes).Humanize()} / {ByteSize.FromBytes(maxBytes).Humanize()} ({storagePath})"
            );
            return 0;
        }
    }
}
