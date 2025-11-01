using System.CommandLine;
using DiscordMusic.Client.Cache;
using DiscordMusic.Client.Lyrics;
using DiscordMusic.Client.Spotify;
using DiscordMusic.Client.YouTube;
using DiscordMusic.Core;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Client.Music;

public static class DiscordMusicCommand
{
    public static RootCommand Create(string[] args)
    {
        var root = new RootCommand("DiscordMusic");
        root.AddCommand(SpotifyCommand.Create(args));
        root.AddCommand(YouTubeCommand.Create(args));
        root.AddCommand(LyricsCommand.Create(args));
        root.AddCommand(CacheCommand.Create(args));

        root.SetHandler(async ctx =>
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.Sources.Clear();
            builder.AddCore(ctx.GetCancellationToken());
            var host = builder.Build();
            host.UseCore();
            await host.RunAsync(ctx.GetCancellationToken());
        });

        return root;
    }
}
