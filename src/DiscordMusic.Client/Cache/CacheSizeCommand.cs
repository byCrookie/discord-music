using System.CommandLine;
using System.IO.Abstractions;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Client.Cache;

public static class CacheSizeCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("size", "Get the size of the cache");
        command.SetAction(async (pr, ct) => await SizeAsync(args, pr, ct));
        return command;
    }

    private static async Task SizeAsync(
        string[] args,
        ParseResult parseResult,
        CancellationToken ct
    )
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddUtils();
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddCache();
        var host = builder.Build();
        var musicCache = host.Services.GetRequiredService<IMusicCache>();
        var size = await musicCache.GetSizeAsync(ct);

        if (size.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(size.ToContent());
            return;
        }

        await parseResult.InvocationConfiguration.Output.WriteLineAsync(
            $"Cache size is {size.Value}"
        );
    }
}
