using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Pipelines;
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

        var match = ErrorRegex().Match(e.Data);

        if (match.Success)
        {
            logger.LogError("{Message}", e.Data);
            errors.Add(match.Groups["Error"].Value);
        }
        else
        {
            logger.LogTrace("{Message}", e.Data);
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

        return new PipelineStream(ytdlpProcess, ffmpegProcess, ct, logger);
    }

    private class PipelineStream : Stream
    {
        private readonly Process _ytDlpProcess;
        private readonly Process _ffmpegProcess;
        private readonly Stream _outputStream; // Exposes ffmpeg's processed output.
        private readonly Task _pumpingTask;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public PipelineStream(Process ytDlpProcess, Process ffmpegProcess, CancellationToken externalToken,
            ILogger logger)
        {
            _ytDlpProcess = ytDlpProcess;
            _ffmpegProcess = ffmpegProcess;
            // Link an internal CTS with the external token.
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            // Create a pipe to shuttle data from yt-dlp's output to ffmpeg's input.
            var inputPipe = new Pipe();
            // Create a pipe to capture ffmpeg's output.
            var outputPipe = new Pipe();

            // Task 1: Read from yt-dlp's stdout and write into the input pipe.
            var pumpInput = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var memory = inputPipe.Writer.GetMemory(4096);
                        var bytesRead = await _ytDlpProcess.StandardOutput.BaseStream.ReadAsync(memory, _cts.Token);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        inputPipe.Writer.Advance(bytesRead);
                        var result = await inputPipe.Writer.FlushAsync(_cts.Token);
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    logger.LogTrace("yt-dlp output completed.");
                    await inputPipe.Writer.CompleteAsync();
                }
            }, _cts.Token);

            // Task 2: Read from the input pipe and write into ffmpeg's stdin.
            var pipeToFfmpeg = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var result = await inputPipe.Reader.ReadAsync(_cts.Token);
                        var buffer = result.Buffer;
                        if (buffer.Length > 0)
                        {
                            foreach (var segment in buffer)
                            {
                                await _ffmpegProcess.StandardInput.BaseStream.WriteAsync(segment, _cts.Token);
                            }
                        }

                        inputPipe.Reader.AdvanceTo(buffer.End);
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    logger.LogTrace("yt-dlp output completed.");
                    await inputPipe.Reader.CompleteAsync();
                    _ffmpegProcess.StandardInput.Close();
                }
            }, _cts.Token);

            // Task 3: Read from ffmpeg's stdout and write into the output pipe.
            var pumpOutput = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var memory = outputPipe.Writer.GetMemory(4096);
                        var bytesRead = await _ffmpegProcess.StandardOutput.BaseStream.ReadAsync(memory, _cts.Token);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        outputPipe.Writer.Advance(bytesRead);
                        var result = await outputPipe.Writer.FlushAsync(_cts.Token);
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    logger.LogTrace("ffmpeg output completed.");
                    await outputPipe.Writer.CompleteAsync();
                }
            }, _cts.Token);

            // Combine the three pumping tasks.
            _pumpingTask = Task.WhenAll(pumpInput, pipeToFfmpeg, pumpOutput);
            // Expose the output pipe's reader as the Stream.
            _outputStream = outputPipe.Reader.AsStream();
        }

        public override bool CanRead => _outputStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => -1;

        public override long Position
        {
            get => -1;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _outputStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _outputStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Cancel all background work.
                _cts.Cancel();
                try
                {
                    // Wait for all pumping tasks to finish.
                    _pumpingTask.Wait();
                }
                catch
                {
                    /* Suppress exceptions on dispose. */
                }

                _outputStream.Dispose();
                // Ensure both processes are terminated.
                if (!_ytDlpProcess.HasExited)
                {
                    try
                    {
                        _ytDlpProcess.Kill(true);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                _ytDlpProcess.Dispose();
                if (!_ffmpegProcess.HasExited)
                {
                    try
                    {
                        _ffmpegProcess.Kill(true);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                _ffmpegProcess.Dispose();
                _cts.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }

    [GeneratedRegex("ERROR.*: (?<Error>.*)")]
    private static partial Regex ErrorRegex();
}
