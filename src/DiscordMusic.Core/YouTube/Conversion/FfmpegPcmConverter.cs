using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Abstractions;
using DiscordMusic.Core.Observability;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.YouTube.Conversion;

internal sealed class FfmpegPcmConverter(
    ILogger<FfmpegPcmConverter> logger,
    YouTubeToolLocations toolLocations,
    ICliCommandRunner commandRunner
) : IAudioConverter
{
    private static readonly Counter<long> AudioConversions =
        DiscordMusicObservability.Meter.CreateCounter<long>(
            "discord.music.audio.conversions",
            unit: "1",
            description: "Audio conversion runs."
        );

    public async Task<ErrorOr<Success>> ConvertToPcmAsync(
        IFileInfo input,
        IFileInfo output,
        CancellationToken ct
    )
    {
        using var activity = DiscordMusicObservability.StartActivity(
            "audio.convert",
            ActivityKind.Client
        );
        DiscordMusicObservability.SetTag(activity, "audio.output.format", "pcm");
        var loadedLocations = toolLocations.Value;

        var arguments = new[]
        {
            "-y",
            "-i",
            input.FullName,
            "-f",
            BitConverter.IsLittleEndian ? "f32le" : "f32be",
            "-ar",
            "48000",
            "-ac",
            "2",
            output.FullName,
        };

        var metricResult = "completed";
        try
        {
            var result = await commandRunner.RunAsync(
                loadedLocations.Ffmpeg.PathToFile,
                arguments,
                null,
                ct
            );

            if (result.ExitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogDebug(
                    "Converted audio {Input} to PCM {Output}.",
                    input.FullName,
                    output.FullName
                );
                return Result.Success;
            }

            metricResult = "failed";
            activity?.SetStatus(ActivityStatusCode.Error, "non_zero_exit_code");
            DiscordMusicObservability.SetTag(activity, "process.exit_code", result.ExitCode);
            logger.LogError(
                "Audio conversion failed. ExitCode={ExitCode} Input={Input} Output={Output} Error={Error}",
                result.ExitCode,
                input.FullName,
                output.FullName,
                result.StandardError
            );

            return Error
                .Unexpected(
                    code: "Audio.ConvertFailed",
                    description: "Converting the downloaded audio failed."
                )
                .WithMetadata(ErrorExtensions.MetadataKeys.Operation, "audio.convert")
                .WithMetadata("exitCode", result.ExitCode)
                .WithMetadata("stderr", result.StandardError)
                .WithMetadata("input", input.FullName)
                .WithMetadata("output", output.FullName);
        }
        catch (OperationCanceledException)
        {
            metricResult = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Error, metricResult);
            throw;
        }
        catch (Exception ex)
        {
            metricResult = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            AudioConversions.Add(1, Tags(metricResult));
        }
    }

    private static TagList Tags(string result)
    {
        return new TagList { { "audio.output.format", "pcm" }, { "result", result } };
    }
}
