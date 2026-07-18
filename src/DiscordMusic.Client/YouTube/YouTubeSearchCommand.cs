using System.CommandLine;
using System.CommandLine.Invocation;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Searching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.YouTube;

public sealed class YouTubeSearchCommand : Command
{
    public YouTubeSearchCommand(string[] args)
        : base("search", "Search for a track on YouTube")
    {
        Add(QueryArgument);

        Action = new YouTubeSearchAction(args);
    }

    private class YouTubeSearchAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            var query = parseResult.GetRequiredValue(QueryArgument);

            var builder = Host.CreateApplicationBuilder(args);
            builder.AddUtils();
            builder.AddYouTubeClient();
            var host = builder.Build();
            var youtubeSearch = host.Services.GetRequiredService<IYouTubeSearch>();
            var search = await youtubeSearch.SearchAsync(query, cancellationToken);

            if (search.IsError)
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                    search.ToErrorContent()
                );
                return 1;
            }

            foreach (var track in search.Value)
            {
                await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                    $"{track.Title} by {track.Channel} - {track.Url}"
                );
            }
            return 0;
        }
    }

    private static Argument<string> QueryArgument { get; } =
        new("query") { Description = "The query to search for. Urls are also supported." };
}
