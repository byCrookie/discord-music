using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using DiscordMusic.Core.Spotify;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Spotify;

public static class SpotifySearchCommand
{
    private static Argument<string> QueryArgument { get; } =
        new("query", "The query to search for. Urls are also supported.");

    public static Command Create(string[] args)
    {
        var command = new Command("search", "Search for a track on Spotify");
        command.AddArgument(QueryArgument);
        command.SetHandler(async ctx => await SearchAsync(args, ctx));
        return command;
    }

    private static async Task SearchAsync(string[] args, InvocationContext context)
    {
        var ct = context.GetCancellationToken();
        var query = context.ParseResult.GetValueForArgument(QueryArgument);

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddSpotify();
        var host = builder.Build();
        var spotifySearch = host.Services.GetRequiredService<ISpotifySearch>();
        var search = await spotifySearch.SearchAsync(query, ct);

        if (search.IsError)
        {
            context.Console.Error.WriteLine(search.ToPrint());
            return;
        }

        foreach (var track in search.Value)
        {
            context.Console.Out.WriteLine($"{track.Name} by {string.Join(", ", track.Artists)} - {track.Url}");
        }
    }
}
