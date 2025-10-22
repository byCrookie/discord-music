using System.Text;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

public static class YouTubeModule
{
    public static IHostApplicationBuilder AddYouTube(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<YouTubeOptions>()
            .Bind(builder.Configuration.GetSection(YouTubeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<YouTubeOptions>, ValidateSettingsOptions>()
        );

        builder.Services.AddTransient<IYoutubeSearch, YoutubeSearch>();
        builder.Services.AddTransient<IYouTubeDownload, YouTubeDownload>();

        return builder;
    }

    private sealed class ValidateSettingsOptions(BinaryLocator binaryLocator) : IValidateOptions<YouTubeOptions>
    {
        public ValidateOptionsResult Validate(string? name, YouTubeOptions options)
        {
            StringBuilder? failure = null;

            var ffmpeg = binaryLocator.LocateAndValidate(options.Ffmpeg, "ffmpeg");

            if (ffmpeg.IsError)
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(YouTubeOptions.Ffmpeg)} {ffmpeg.ToPrint()}"
                );
            }

            var ytdlp = binaryLocator.LocateAndValidate(options.Ytdlp, "yt-dlp");

            if (ytdlp.IsError)
            {
                (failure ??= new StringBuilder()).AppendLine(
                    $"{nameof(YouTubeOptions.Ytdlp)} {ytdlp.ToPrint()}"
                );
            }

            return failure is not null ? ValidateOptionsResult.Fail(failure.ToString()) : ValidateOptionsResult.Success;
        }
    }
}
