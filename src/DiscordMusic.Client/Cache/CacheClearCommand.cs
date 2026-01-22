using System.CommandLine;
using System.IO.Abstractions;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Client.Cache;

public static class CacheClearCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("clear", "Clear cache");
        command.SetAction(async (pr, ct) => await ClearAsync(args, pr, ct));
        return command;
    }

    private static async Task ClearAsync(
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

        var clear = await musicCache.ClearAsync(ct);

        if (clear.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(clear.ToErrorContent());
            return;
        }

        await parseResult.InvocationConfiguration.Output.WriteLineAsync("Cache cleared");
    }
}
