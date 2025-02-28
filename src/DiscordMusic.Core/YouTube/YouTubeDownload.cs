using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.YouTube;

internal partial class YouTubeDownload(
    ILogger<YouTubeDownload> logger,
    IOptions<YouTubeOptions> options,
    IFileSystem fileSystem,
    BinaryLocator binaryLocator
) : IYouTubeDownload
{
    public async Task<ErrorOr<Success>> DownloadAsync(string query, IFileInfo output, CancellationToken ct)
    {
        var tempFile = $"{output}.tmp";
        var opusTempFile = $"{tempFile}.opus";

        try
        {
            logger.LogDebug("Downloading audio from YouTube for {Query} to {Output} with temporary file {TempFile}.",
                query, output.FullName, tempFile);

            var ytdlp = binaryLocator.LocateAndValidate(options.Value.Ytdlp, "yt-dlp");

            if (ytdlp.IsError)
            {
                logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToPrint());
                return Error.Unexpected(description: $"Failed to locate yt-dlp: {ytdlp.ToPrint()}");
            }

            var ffmpeg = binaryLocator.LocateAndValidate(options.Value.Ffmpeg, "ffmpeg");

            if (ffmpeg.IsError)
            {
                logger.LogError("Failed to locate ffmpeg: {Error}", ffmpeg.ToPrint());
                return Error.Unexpected(description: $"Failed to locate ffmpeg: {ffmpeg.ToPrint()}");
            }

            var command = new StringBuilder();
            command.Append($"--default-search auto \"{query}\"");
            command.Append(" -f \"bestaudio\"");
            command.Append(" --extract-audio");

            if (ffmpeg.Value.Type == BinaryLocator.LocationType.Resolved)
            {
                command.Append($" --ffmpeg-location \"{ffmpeg.Value.PathToFolder}\"");
            }

            command.Append(" --audio-format opus");
            command.Append(" --audio-quality 0");
            command.Append($" --output \"{tempFile}\"");
            command.Append(" --no-playlist");

            logger.LogDebug("Start process {Ytdlp} with command {Command}.", ytdlp.Value.PathToFile, command);

            using var ytdlpProcess = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ytdlp.Value.PathToFile,
                    Arguments = command.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            );

            if (ytdlpProcess is null)
            {
                logger.LogError("Failed to start process yt-dlp with command {Command}.", command);
                return Error.Unexpected(description: $"Failed to start process yt-dlp with command {command}");
            }

            var lines = new List<string>();
            var errors = new List<string>();

            ytdlpProcess.OutputDataReceived += (_, args) => ProcessOutput(args, lines);
            ytdlpProcess.ErrorDataReceived += (_, args) => ProcessError(args, errors);

            ytdlpProcess.BeginOutputReadLine();
            ytdlpProcess.BeginErrorReadLine();

            await ytdlpProcess.WaitForExitAsync(ct);

            if (ytdlpProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ytdlpProcess.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            logger.LogDebug("YouTube download completed. Converting opus in {TempFile} to raw pcm in {Output}.",
                tempFile, output.FullName);

            var ffmpegArgs =
                $"-i \"{opusTempFile}\" -f s{AudioStream.BitsPerSample}le -ar {AudioStream.SampleRate} -ac {AudioStream.Channels} {output.FullName}";
            logger.LogTrace("Calling {Ffmpeg} with arguments {FfmpegArgs}", ffmpeg.Value.PathToFile, ffmpegArgs);
            using var ffmpegProcess = Process.Start(
                new ProcessStartInfo
                {
                    FileName = ffmpeg.Value.PathToFile,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );

            if (ffmpegProcess is null)
            {
                logger.LogError(
                    "Failed to start process ffmpeg with arguments {FfmpegArgs}.",
                    ffmpegArgs
                );
                return Error.Unexpected(
                    description: $"Failed to start process ffmpeg with arguments {ffmpegArgs}"
                );
            }

            await ffmpegProcess.WaitForExitAsync(ct);

            if (ffmpegProcess.ExitCode != 0)
            {
                var errorMessage = string.Join(Environment.NewLine, errors);
                logger.LogError("YouTube download failed with exit code {ExitCode}", ytdlpProcess.ExitCode);
                return Error.Unexpected(description: $"Download from YouTube for {query} failed: {errorMessage}");
            }

            logger.LogDebug("YouTube download completed. Audio file saved at {Output}.", output.FullName);
            return Result.Success;
        }
        finally
        {
            if (fileSystem.File.Exists(opusTempFile))
            {
                logger.LogTrace("Deleting temporary file {TempFile}", opusTempFile);
                fileSystem.File.Delete(opusTempFile);
            }
        }
    }

    private void ProcessOutput(DataReceivedEventArgs e, List<string> lines)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogTrace("{Message}", e.Data);
        lines.Add(e.Data);
    }

    private void ProcessError(DataReceivedEventArgs e, List<string> errors)
    {
        if (e.Data is null)
        {
            return;
        }

        logger.LogWarning("{Message}", e.Data);
        var match = ErrorRegex().Match(e.Data);

        if (match.Success)
        {
            errors.Add(match.Groups["Error"].Value);
        }
    }

    public ErrorOr<Stream> Stream(string query, CancellationToken ct)
    {
        logger.LogDebug("Streaming audio from YouTube for {Query}.", query);

        var ytdlp = binaryLocator.LocateAndValidate(options.Value.Ytdlp, "yt-dlp");
        if (ytdlp.IsError)
        {
            logger.LogError("Failed to locate yt-dlp: {Error}", ytdlp.ToPrint());
            return Error.Unexpected($"Failed to locate yt-dlp: {ytdlp.ToPrint()}");
        }

        var ffmpeg = binaryLocator.LocateAndValidate(options.Value.Ffmpeg, "ffmpeg");
        if (ffmpeg.IsError)
        {
            logger.LogError("Failed to locate ffmpeg: {Error}", ffmpeg.ToPrint());
            return Error.Unexpected($"Failed to locate ffmpeg: {ffmpeg.ToPrint()}");
        }

        var ytdlpArgs = new StringBuilder()
            .Append($"--default-search auto \"{query}\" ")
            .Append("-f bestaudio ")
            .Append("--extract-audio ");

        if (ffmpeg.Value.Type == BinaryLocator.LocationType.Resolved)
        {
            ytdlpArgs.Append($" --ffmpeg-location \"{ffmpeg.Value.PathToFolder}\"");
        }

        ytdlpArgs.Append("--audio-format opus ")
            .Append("--audio-quality 0 ")
            .Append("--no-playlist ")
            .Append("-o -");

        var ffmpegArgs = new StringBuilder()
            .Append("-i pipe:0 ")
            .Append($"-f s{AudioStream.BitsPerSample}le ")
            .Append($"-ar {AudioStream.SampleRate} ")
            .Append($"-ac {AudioStream.Channels} ")
            .Append("pipe:1");

        logger.LogDebug("Starting yt-dlp and ffmpeg pipeline.");

        var ytdlpProcess = Process.Start(new ProcessStartInfo
        {
            FileName = ytdlp.Value.PathToFile,
            Arguments = ytdlpArgs.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (ytdlpProcess is null)
        {
            logger.LogError("Failed to start process yt-dlp with command {Command}.", ytdlpArgs);
            return Error.Unexpected(description: $"Failed to start process yt-dlp with command {ytdlpArgs}");
        }

        var ytdlpErrors = new List<string>();
        ytdlpProcess.ErrorDataReceived += (_, args) => ProcessError(args, ytdlpErrors);
        ytdlpProcess.BeginErrorReadLine();

        var ffmpegProcess = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpeg.Value.PathToFile,
            Arguments = ffmpegArgs.ToString(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (ffmpegProcess is null)
        {
            logger.LogError("Failed to start process ffmpeg with command {Command}.", ffmpegArgs);
            return Error.Unexpected(description: $"Failed to start process ffmpeg with command {ffmpegArgs}");
        }

        var ffmpegErrors = new List<string>();
        ffmpegProcess.ErrorDataReceived += (_, args) => ProcessError(args, ffmpegErrors);
        ffmpegProcess.BeginErrorReadLine();

        return new PipelineStream(ytdlpProcess, ffmpegProcess, ct);
    }

    private class PipelineStream : Stream
    {
        private readonly Process _output;
        private readonly Process _input;

        public PipelineStream(Process output, Process input, CancellationToken ct)
        {
            _output = output;
            _input = input;

            _ = output.StandardOutput.BaseStream.CopyToAsync(input.StandardInput.BaseStream, ct);
        }

        public override void Flush()
        {
            _output.StandardOutput.BaseStream.Flush();
            _input.StandardInput.BaseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _input.HasExited ? 0 : _input.StandardOutput.BaseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _input.StandardOutput.BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _input.StandardOutput.BaseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _output.StandardInput.BaseStream.Write(buffer, offset, count);
        }

        public override bool CanRead => _input.StandardOutput.BaseStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => -1;

        public override long Position
        {
            get => -1;
            set { }
        }

        public override void Close()
        {
            _output.Kill(true);
            _output.Dispose();
            _input.Kill(true);
            _input.Dispose();

            base.Close();
        }
    }

    [GeneratedRegex("ERROR.*: (?<Error>.*)")]
    private static partial Regex ErrorRegex();
}
