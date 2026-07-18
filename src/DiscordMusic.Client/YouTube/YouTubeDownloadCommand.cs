using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Downloading;
using Flurl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Client.YouTube;

public sealed class YouTubeDownloadCommand : Command
{
    public YouTubeDownloadCommand(string[] args)
        : base("download", "Download a track from YouTube")
    {
        Add(UrlArgument);
        Add(OutputArgument);

        Action = new YouTubeDownloadAction(args);
    }

    private class YouTubeDownloadAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            var url = parseResult.GetRequiredValue(UrlArgument);
            var output = parseResult.GetRequiredValue(OutputArgument);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
            builder.AddUtils();
            builder.AddYouTubeClient();
            var host = builder.Build();
            var fileSystem = host.Services.GetRequiredService<IFileSystem>();
            var youTubeDownload = host.Services.GetRequiredService<IYouTubeDownload>();
            var download = await youTubeDownload.DownloadAsync(
                new Url(url),
                fileSystem.FileInfo.Wrap(output),
                cancellationToken
            );

            if (download.IsError)
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                    download.ToErrorContent()
                );
                return 1;
            }

            await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                $"Downloaded track to {output.FullName}"
            );
            return 0;
        }
    }

    private static Argument<string> UrlArgument { get; } =
        new("url") { Description = "The url of the track to download from YouTube" };

    private static Argument<FileInfo> OutputArgument { get; } =
        new("output")
        {
            Description =
                "The output file path + name to save the track to. Track will be saved as raw PCM audio.",
        };
}
