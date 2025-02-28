using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
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
        new(["--title", "-t"], "The title of the track") { IsRequired = true };

    private static Option<string> ArtistOption { get; } =
        new(["--artist", "-a"], "The artist of the track") { IsRequired = true };

    private static Option<string> UrlOption { get; } =
        new(["--url", "-u"], "The URL of the track") { IsRequired = true };

    private static Option<TimeSpan> DurationOption { get; } =
        new(["--duration", "-d"], () => TimeSpan.Zero, "The duration of the track");

    public static Command Create(string[] args)
    {
        var command = new Command("it", "Get or add track to the cache");
        command.AddOption(TitleOption);
        command.AddOption(ArtistOption);
        command.AddOption(UrlOption);
        command.AddOption(DurationOption);
        command.SetHandler(async ctx => await ReserveAsync(args, ctx));
        return command;
    }

    private static async Task ReserveAsync(string[] args, InvocationContext context)
    {
        var ct = context.GetCancellationToken();
        var title = context.ParseResult.GetValueForOption(TitleOption);
        var artist = context.ParseResult.GetValueForOption(ArtistOption);
        var url = context.ParseResult.GetValueForOption(UrlOption);
        var duration = context.ParseResult.GetValueForOption(DurationOption);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(url))
        {
            context.Console.Error.WriteLine("Title, artist, and URL are required");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddUtils();
        builder.Services.AddSingleton<IFileSystem>(new RealFileSystem());
        builder.AddCache();
        var host = builder.Build();
        var musicCache = host.Services.GetRequiredService<IMusicCache>();
        var file = await musicCache.GetOrAddTrackAsync(new Track(title, artist, url, duration),
            AudioStream.ApproxSize(duration), ct);

        if (file.IsError)
        {
            context.Console.Error.WriteLine(file.ToPrint());
            return;
        }

        context.Console.Out.WriteLine($"Track added to cache: {file.Value}");
    }
}
