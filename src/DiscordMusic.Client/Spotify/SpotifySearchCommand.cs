using System.CommandLine;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Spotify;

public static class SpotifySearchCommand
{
    private static Argument<string> QueryArgument { get; } =
        new("query") { Description = "The query to search for. Urls are also supported." };

    public static Command Create(string[] args)
    {
        var command = new Command("search", "Search for a track on Spotify") { QueryArgument };
        command.SetAction(async (pr, ct) => await SearchAsync(args, pr, ct));
        return command;
    }

    private static async Task SearchAsync(
        string[] args,
        ParseResult parseResult,
        CancellationToken ct
    )
    {
        var query = parseResult.GetRequiredValue(QueryArgument);

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddSpotify();
        var host = builder.Build();
        var spotifySearch = host.Services.GetRequiredService<ISpotifySearch>();
        var search = await spotifySearch.SearchAsync(query, ct);

        if (search.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(search.ToPrint());
            return;
        }

        foreach (var track in search.Value)
        {
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                $"{track.Name} by {string.Join(", ", track.Artists)} - {track.Url}"
            );
        }
    }
}
