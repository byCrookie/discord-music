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
        var root = new RootCommand("DiscordMusic")
        {
            SpotifyCommand.Create(args),
            YouTubeCommand.Create(args),
            LyricsCommand.Create(args),
            CacheCommand.Create(args),
        };

        root.SetAction(
            async (pr, ct) =>
            {
                var builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.Sources.Clear();
                builder.AddCore(ct);
                var host = builder.Build();
                host.UseCore();
                await host.RunAsync(ct);
            }
        );

        return root;
    }
}
