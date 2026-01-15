using System.CommandLine;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.YouTube;

public static class YouTubeSearchCommand
{
    private static Argument<string> QueryArgument { get; } =
        new("query") { Description = "The query to search for. Urls are also supported." };

    public static Command Create(string[] args)
    {
        var command = new Command("search", "Search for a track on YouTube") { QueryArgument };
        command.SetAction(async (pr, ct) => await SearchAsync(args, pr, ct));
        return command;
    }

    private static async Task SearchAsync(string[] args, ParseResult parseResult, CancellationToken ct)
    {
        var query = parseResult.GetRequiredValue(QueryArgument);

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddUtils();
        builder.AddYouTube();
        var host = builder.Build();
        var youtubeSearch = host.Services.GetRequiredService<IYoutubeSearch>();
        var search = await youtubeSearch.SearchAsync(query, ct);

        if (search.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(search.ToPrint());
            return;
        }

        foreach (var track in search.Value)
        {
            await parseResult.InvocationConfiguration.Output.WriteLineAsync($"{track.Title} by {track.Channel} - {track.Url}");
        }
    }
}
