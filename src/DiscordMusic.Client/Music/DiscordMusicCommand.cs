using System.CommandLine;
using System.CommandLine.Invocation;
using DiscordMusic.Client.Lyrics;
using DiscordMusic.Client.Spotify;
using DiscordMusic.Client.Storage;
using DiscordMusic.Client.YouTube;
using DiscordMusic.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Client.Music;

public sealed class DiscordMusicCommand : RootCommand
{
    public DiscordMusicCommand(string[] args)
        : base("DiscordMusic")
    {
        Add(EnvFileOption);
        Add(new YouTubeCommand(args));
        Add(new SpotifyCommand(args));
        Add(new LyricsCommand(args));
        Add(new StorageCommand(args));

        Action = new DiscordMusicCommandAction(args);
    }

    private class DiscordMusicCommandAction(string[] args) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken = new()
        )
        {
            using var factory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
            );
            var logger = factory.CreateLogger(nameof(DiscordMusicCommand));
            var dotEnvPath = parseResult.GetRequiredValue(EnvFileOption);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.Sources.Clear();
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.AddYouTubeClient();
            builder.AddCore(logger, dotEnvPath, cancellationToken);
            var host = builder.Build();
            host.UseCore();
            await host.RunAsync(cancellationToken);
            return 0;
        }
    }

    private static Option<string> EnvFileOption { get; } =
        new("--env-file")
        {
            Description = "The .env file to load for Discord Music configuration.",
            Recursive = true,
            DefaultValueFactory = _ => ".env",
            Required = false,
        };
}
