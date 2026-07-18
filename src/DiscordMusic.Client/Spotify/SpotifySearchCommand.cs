using System.CommandLine;
using System.CommandLine.Invocation;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Spotify;

public sealed class SpotifySearchCommand : Command
{
    public SpotifySearchCommand(string[] args)
        : base("search", "Search for a track on Spotify")
    {
        Add(QueryArgument);

        Action = new SpotifySearchAction(args);
    }

    private class SpotifySearchAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            var query = parseResult.GetRequiredValue(QueryArgument);

            var builder = Host.CreateApplicationBuilder(args);
            builder.AddSpotify();
            var host = builder.Build();
            var spotifySearch = host.Services.GetRequiredService<ISpotifySearch>();
            var search = await spotifySearch.SearchAsync(query, cancellationToken);

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
                    $"{track.Name} by {string.Join(", ", track.Artists)} - {track.Url}"
                );
            }
            return 0;
        }
    }

    private static Argument<string> QueryArgument { get; } =
        new("query") { Description = "The query to search for. Urls are also supported." };
}
