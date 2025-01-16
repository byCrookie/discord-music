using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
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
        command.SetHandler(async ctx => await ClearAsync(args, ctx));
        return command;
    }

    private static async Task ClearAsync(string[] args, InvocationContext context)
    {
        var ct = context.GetCancellationToken();

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddUtils();
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddCache();
        var host = builder.Build();
        var musicCache = host.Services.GetRequiredService<IMusicCache>();

        var clear = await musicCache.ClearAsync(ct);

        if (clear.IsError)
        {
            context.Console.Error.WriteLine(clear.ToPrint());
            return;
        }

        context.Console.Out.WriteLine("Cache cleared");
    }
}
