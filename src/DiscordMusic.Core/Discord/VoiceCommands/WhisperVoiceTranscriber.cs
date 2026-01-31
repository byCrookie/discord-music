using System.Buffers.Binary;
using DiscordMusic.Core.Config;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class WhisperVoiceTranscriber : IVoiceTranscriber, IDisposable
{
    private readonly ILogger<WhisperVoiceTranscriber> _logger;
    private readonly AppPaths _appPaths;
    private readonly WhisperFactory _factory;

    public WhisperVoiceTranscriber(ILogger<WhisperVoiceTranscriber> logger, AppPaths appPaths)
    {
        _logger = logger;
        _appPaths = appPaths;

        _factory = DownloadModel().GetAwaiter().GetResult();
    }

    public async Task<string> TranscribeAsync(
        ReadOnlyMemory<byte> pcm16kMonoS16,
        CancellationToken ct
    )
    {
        // Whisper.net expects a WAV stream for ProcessAsync, easiest is to wrap our PCM in a minimal WAV header.
        await using var wav = new MemoryStream(capacity: pcm16kMonoS16.Length + 64);
        WriteWavHeader(
            wav,
            pcm16kMonoS16.Length,
            sampleRate: 16000,
            channels: 1,
            bitsPerSample: 16
        );
        await wav.WriteAsync(pcm16kMonoS16, ct);
        wav.Position = 0;

        await using var processor = _factory.CreateBuilder().WithLanguage("auto").Build();

        var sb = new System.Text.StringBuilder();
        await foreach (var segment in processor.ProcessAsync(wav, ct))
        {
            if (!string.IsNullOrWhiteSpace(segment.Text))
            {
                sb.Append(segment.Text.Trim());
                sb.Append(' ');
            }
        }

        return sb.ToString().Trim();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<WhisperFactory> DownloadModel()
    {
        var cache = _appPaths.Cache();
        var modelPath = Path.Combine(cache.FullName, "ggml-base.bin");

        if (!File.Exists(modelPath))
        {
            _logger.LogInformation(
                "Whisper model {ModelPath} not found; downloading {Type}...",
                modelPath,
                GgmlType.Base
            );
            await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                GgmlType.Base
            );
            await using var fileWriter = File.OpenWrite(modelPath);
            await modelStream.CopyToAsync(fileWriter);
        }

        return WhisperFactory.FromPath(modelPath);
    }

    private static void WriteWavHeader(
        Stream stream,
        int pcmDataBytes,
        int sampleRate,
        short channels,
        short bitsPerSample
    )
    {
        // RIFF header
        Span<byte> header = stackalloc byte[44];
        header[0] = (byte)'R';
        header[1] = (byte)'I';
        header[2] = (byte)'F';
        header[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), 36 + pcmDataBytes);
        header[8] = (byte)'W';
        header[9] = (byte)'A';
        header[10] = (byte)'V';
        header[11] = (byte)'E';

        // fmt
        header[12] = (byte)'f';
        header[13] = (byte)'m';
        header[14] = (byte)'t';
        header[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), 16); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(20, 2), 1); // format = PCM
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(22, 2), channels);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(24, 4), sampleRate);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(header.Slice(34, 2), bitsPerSample);

        // data
        header[36] = (byte)'d';
        header[37] = (byte)'a';
        header[38] = (byte)'t';
        header[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(40, 4), pcmDataBytes);

        stream.Write(header);
    }
}
