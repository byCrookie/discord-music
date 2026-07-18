using System.Text;
using System.Threading.Channels;
using DiscordMusic.Core.Queues;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.YouTube.Conversion;
using DiscordMusic.Core.YouTube.Downloading;
using DiscordMusic.Core.YouTube.Searching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

internal static class YouTubeModule
{
    public static void AddYouTube(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<YouTubeOptions>()
            .Bind(builder.Configuration.GetSection(YouTubeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<YouTubeOptions>, ValidateSettingsOptions>()
        );

        builder.Services.AddSingleton<YouTubeToolLocations>();
        builder.Services.AddTransient<IYouTubeSearch, YouTubeSearch>();
        builder.Services.AddTransient<IYouTubeAudioDownloader, YouTubeAudioDownloader>();
        builder.Services.AddTransient<IAudioConverter, FfmpegPcmConverter>();
        builder.Services.AddTransient<IYouTubeDownload, YouTubeDownload>();
        builder.Services.AddSingleton<IYouTubeDownloadScheduler, YouTubeDownloadScheduler>();
        builder.Services.AddTransient<
            IYouTubeSearchRequestProcessor,
            YouTubeSearchRequestProcessor
        >();
        builder.Services.AddTransient<
            IYouTubeDownloadRequestProcessor,
            YouTubeDownloadRequestProcessor
        >();

        var ytRequestQueue = new BackgroundQueue<YouTubeSearchRequest>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true,
            }
        );

        builder.Services.AddSingleton<IBackgroundQueue<YouTubeSearchRequest>>(ytRequestQueue);

        var ytDownloadQueue = new BackgroundQueue<YouTubeDownloadRequest>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true,
            }
        );

        builder.Services.AddSingleton<IBackgroundQueue<YouTubeDownloadRequest>>(ytDownloadQueue);
    }

    private sealed class ValidateSettingsOptions(
        YouTubeToolLocations toolLocations,
        ILogger<ValidateSettingsOptions> logger
    ) : IValidateOptions<YouTubeOptions>
    {
        public ValidateOptionsResult Validate(string? name, YouTubeOptions options)
        {
            StringBuilder? failure = null;
            var loadedLocations = toolLocations.Load(options);

            if (loadedLocations.Ffmpeg.IsError)
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(YouTubeOptions.Ffmpeg)} {loadedLocations.Ffmpeg.ToErrorContent()}"
                );
            }
            else
            {
                LogBinaryLocation("ffmpeg", loadedLocations.Ffmpeg.Value);
            }

            if (loadedLocations.Deno.IsError)
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(YouTubeOptions.Deno)} {loadedLocations.Deno.ToErrorContent()}"
                );
            }
            else
            {
                LogBinaryLocation("deno", loadedLocations.Deno.Value);
            }

            if (loadedLocations.Ytdlp.IsError)
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(YouTubeOptions.Ytdlp)} {loadedLocations.Ytdlp.ToErrorContent()}"
                );
            }
            else
            {
                LogBinaryLocation("yt-dlp", loadedLocations.Ytdlp.Value);
            }

            logger.LogInformation(
                "YouTube tool options loaded. JsRuntimes={JsRuntimes}, RemoteComponents={RemoteComponents}, NoJsRuntimes={NoJsRuntimes}, NoRemoteComponents={NoRemoteComponents}",
                string.Join(",", options.JsRuntimes),
                string.Join(",", options.RemoteComponents),
                options.NoJsRuntimes,
                options.NoRemoteComponents
            );

            return failure is not null
                ? ValidateOptionsResult.Fail(failure.ToString())
                : ValidateOptionsResult.Success;
        }

        private void LogBinaryLocation(string binaryName, BinaryLocator.BinaryLocation location)
        {
            if (location.Type == BinaryLocator.LocationType.Runtime)
            {
                logger.LogInformation(
                    "Using runtime-resolved {BinaryName} binary from PATH.",
                    binaryName
                );
                return;
            }

            logger.LogInformation(
                "Using configured {BinaryName} binary {BinaryPath}.",
                binaryName,
                location.PathToFile
            );
        }
    }
}
