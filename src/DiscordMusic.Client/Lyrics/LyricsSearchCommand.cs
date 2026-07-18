using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using DiscordMusic.Core.Lyrics;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Lyrics;

public sealed class LyricsSearchCommand : Command
{
    public LyricsSearchCommand(string[] args)
        : base("search", "Search for lyrics")
    {
        Add(TitleOption);
        Add(ArtistOption);

        Action = new LyricsSearchAction(args);
    }

    private class LyricsSearchAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            var title = parseResult.GetRequiredValue(TitleOption);
            var artist = parseResult.GetRequiredValue(ArtistOption);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                    "Title and artist are required"
                );
                return 1;
            }

            var builder = Host.CreateApplicationBuilder(args);
            builder.AddLyrics();
            var host = builder.Build();
            var lyricsSearch = host.Services.GetRequiredService<ILyricsSearch>();
            var search = await lyricsSearch.SearchAsync(title, artist, cancellationToken);

            if (search.IsError)
            {
                await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                    search.ToErrorContent()
                );
                return 1;
            }

            var lyricsOutput = new StringBuilder();
            lyricsOutput.AppendLine($"Lyrics for {search.Value.Title} - {search.Value.Artist}");
            lyricsOutput.AppendLine(search.Value.Text);
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(
                lyricsOutput.ToString()
            );
            return 0;
        }
    }

    private static Option<string> TitleOption { get; } =
        new("--title", "-t")
        {
            Required = true,
            Description = "The title of the track to search for",
        };

    private static Option<string> ArtistOption { get; } =
        new("--artist", "-a")
        {
            Required = true,
            Description = "The artist of the track to search for",
        };
}
