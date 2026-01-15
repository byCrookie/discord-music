using System.CommandLine;
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
    private static Argument<string> UrlArgument { get; } = new("url")
        { Description = "The url of the track to download from YouTube" };

    private static Argument<FileInfo> OutputArgument { get; } =
        new("output") {
            Description =
                "The output file path + name to save the track to. Track will be saved as an opus file with the .opus extension."
        };

    public static Command Create(string[] args)
    {
        var command = new Command("download", "Download a track from YouTube")
        {
            UrlArgument,
            OutputArgument
        };
        command.SetAction(async (pr, ct) => await DownloadAsync(args, pr, ct));
        return command;
    }

    private static async Task DownloadAsync(string[] args, ParseResult parseResult, CancellationToken ct)
    {
        var url = parseResult.GetRequiredValue(UrlArgument);
        var output = parseResult.GetRequiredValue(OutputArgument);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddUtils();
        builder.AddYouTube();
        var host = builder.Build();
        var fileSystem = host.Services.GetRequiredService<IFileSystem>();
        var youTubeDownload = host.Services.GetRequiredService<IYouTubeDownload>();
        var download = await youTubeDownload.DownloadAsync(new Url(url), fileSystem.FileInfo.Wrap(output), ct);

        if (download.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(download.ToPrint());
            return;
        }

        await parseResult.InvocationConfiguration.Output.WriteLineAsync($"Downloaded track to {output.FullName}");
    }
}
