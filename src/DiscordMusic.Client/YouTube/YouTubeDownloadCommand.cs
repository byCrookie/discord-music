using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Flurl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Client.YouTube;

public static class YouTubeDownloadCommand
{
    private static Argument<string> UrlArgument { get; } = new("url", "The url of the track to download from YouTube");

    private static Argument<FileInfo> OutputArgument { get; } =
        new(
            "output",
            "The output file path + name to save the track to. Track will be saved as an opus file with the .opus extension."
        );

    public static Command Create(string[] args)
    {
        var command = new Command("download", "Download a track from YouTube");
        command.AddArgument(UrlArgument);
        command.AddArgument(OutputArgument);
        command.SetHandler(async ctx => await DownloadAsync(args, ctx));
        return command;
    }

    private static async Task DownloadAsync(string[] args, InvocationContext context)
    {
        var ct = context.GetCancellationToken();
        var url = context.ParseResult.GetValueForArgument(UrlArgument);
        var output = context.ParseResult.GetValueForArgument(OutputArgument);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddUtils();
        builder.AddYouTube();
        var host = builder.Build();
        var fileSystem = host.Services.GetRequiredService<IFileSystem>();
        var youTubeDownload = host.Services.GetRequiredService<IYouTubeDownload>();
        var donwload = await youTubeDownload.DownloadAsync(new Url(url), fileSystem.FileInfo.Wrap(output), ct);

        if (donwload.IsError)
        {
            context.Console.Error.WriteLine(donwload.ToPrint());
            return;
        }

        context.Console.Out.WriteLine($"Downloaded track to {output.FullName}");
    }
}
