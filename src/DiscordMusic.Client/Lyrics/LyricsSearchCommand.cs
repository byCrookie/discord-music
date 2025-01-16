using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Text;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Lyrics;

public static class LyricsSearchCommand
{
    private static Option<string> TitleOption { get; } =
        new(["--title", "-t"], "The title of the track to search for") { IsRequired = true };

    private static Option<string> ArtistOption { get; } =
        new(["--artist", "-a"], "The artist of the track to search for") { IsRequired = true };

    public static Command Create(string[] args)
    {
        var command = new Command("search", "Search for lyrics");
        command.AddOption(TitleOption);
        command.AddOption(ArtistOption);
        command.SetHandler(async ctx => await SearchAsync(args, ctx));
        return command;
    }

    private static async Task SearchAsync(string[] args, InvocationContext context)
    {
        var ct = context.GetCancellationToken();
        var title = context.ParseResult.GetValueForOption(TitleOption);
        var artist = context.ParseResult.GetValueForOption(ArtistOption);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            context.Console.Error.WriteLine("Title and artist are required");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddLyrics();
        var host = builder.Build();
        var lyricsSearch = host.Services.GetRequiredService<ILyricsSearch>();
        var search = await lyricsSearch.SearchAsync(title, artist, ct);

        if (search.IsError)
        {
            context.Console.Error.WriteLine(search.ToPrint());
            return;
        }

        var lyricsOutput = new StringBuilder();
        lyricsOutput.AppendLine($"Lyrics for {search.Value.Title} - {search.Value.Artist} ({search.Value.Url})");
        lyricsOutput.AppendLine(search.Value.Text);
        context.Console.Out.WriteLine(lyricsOutput.ToString());
    }
}
