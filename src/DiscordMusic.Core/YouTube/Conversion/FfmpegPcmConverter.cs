using System.IO.Abstractions;
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
    public async Task<ErrorOr<Success>> ConvertToPcmAsync(
        IFileInfo input,
        IFileInfo output,
        CancellationToken ct
    )
    {
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

        var result = await commandRunner.RunAsync(
            loadedLocations.Ffmpeg.PathToFile,
            arguments,
            null,
            ct
        );

        if (result.ExitCode == 0)
        {
            logger.LogDebug(
                "Converted audio {Input} to PCM {Output}.",
                input.FullName,
                output.FullName
            );
            return Result.Success;
        }

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
}
