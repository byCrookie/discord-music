using System.CommandLine;
using System.Text;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Lyrics;

public static class LyricsSearchCommand
{
    private static Option<string> TitleOption { get; } =
        new("--title", "-t") { Required = true, Description = "The title of the track to search for" };

    private static Option<string> ArtistOption { get; } =
        new("--artist", "-a") { Required = true, Description = "The artist of the track to search for"};

    public static Command Create(string[] args)
    {
        var command = new Command("search", "Search for lyrics")
        {
            TitleOption,
            ArtistOption
        };
        command.SetAction(async (pr, ct) => await SearchAsync(args, pr, ct));
        return command;
    }

    private static async Task SearchAsync(string[] args, ParseResult parseResult, CancellationToken ct)
    {
        var title = parseResult.GetRequiredValue(TitleOption);
        var artist = parseResult.GetRequiredValue(ArtistOption);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync("Title and artist are required");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddLyrics();
        var host = builder.Build();
        var lyricsSearch = host.Services.GetRequiredService<ILyricsSearch>();
        var search = await lyricsSearch.SearchAsync(title, artist, ct);

        if (search.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(search.ToPrint());
            return;
        }

        var lyricsOutput = new StringBuilder();
        lyricsOutput.AppendLine($"Lyrics for {search.Value.Title} - {search.Value.Artist} ({search.Value.Url})");
        lyricsOutput.AppendLine(search.Value.Text);
        await parseResult.InvocationConfiguration.Output.WriteLineAsync(lyricsOutput.ToString());
    }
}
