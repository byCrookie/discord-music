using System.CommandLine;
using System.IO.Abstractions;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Cache;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testably.Abstractions;

namespace DiscordMusic.Client.Cache;

public static class CacheGetOrAddCommand
{
    private static Option<string> TitleOption { get; } =
        new("--title", "-t") { Required = true, Description = "The title of the track" };

    private static Option<string> ArtistOption { get; } =
        new("--artist", "-a") { Required = true, Description = "The artist of the track" };

    private static Option<string> UrlOption { get; } =
        new("--url", "-u") { Required = true, Description = "The URL of the track" };

    private static Option<TimeSpan> DurationOption { get; } =
        new("--duration", "-d")
        {
            DefaultValueFactory = _ => TimeSpan.Zero,
            Description = "The duration of the track",
        };

    public static Command Create(string[] args)
    {
        var command = new Command("it", "Get or add track to the cache")
        {
            TitleOption,
            ArtistOption,
            UrlOption,
            DurationOption,
        };
        command.SetAction(async (pr, ct) => await ReserveAsync(args, pr, ct));
        return command;
    }

    private static async Task ReserveAsync(
        string[] args,
        ParseResult parseResult,
        CancellationToken ct
    )
    {
        var title = parseResult.GetRequiredValue(TitleOption);
        var artist = parseResult.GetRequiredValue(ArtistOption);
        var url = parseResult.GetRequiredValue(UrlOption);
        var duration = parseResult.GetRequiredValue(DurationOption);

        if (
            string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(artist)
            || string.IsNullOrWhiteSpace(url)
        )
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                "Title, artist, and URL are required"
            );
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddUtils();
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddCache();
        var host = builder.Build();
        var musicCache = host.Services.GetRequiredService<IMusicCache>();
        var file = await musicCache.GetOrAddTrackAsync(
            new Track(title, artist, url, duration),
            AudioStream.ApproxSize(duration),
            ct
        );

        if (file.IsError)
        {
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(file.ToContent());
            return;
        }

        await parseResult.InvocationConfiguration.Output.WriteLineAsync(
            $"Track added to cache: {file.Value}"
        );
    }
}
