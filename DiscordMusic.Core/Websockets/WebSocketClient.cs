using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Websockets;

public sealed class WebSocketClient(Uri uri, ILogger<WebSocketClient> logger, int bufferSize = 8192) : IAsyncDisposable
{
    private readonly ClientWebSocket _ws = new();

    public async IAsyncEnumerable<string> ConnectAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await _ws.ConnectAsync(uri, ct);

        while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var outputStream = new MemoryStream();

            try
            {
                try
                {
                    outputStream = await ReceiveAsync(_ws, bufferSize, ct);
                }
                catch (TaskCanceledException)
                {
                    yield break;
                }

                yield return await OnReceivedAsync(outputStream);
            }
            finally
            {
                await outputStream.DisposeAsync();
            }
        }

        if (_ws.State != WebSocketState.Open)
        {
            logger.LogWarning("Websocket was closed unexpectedly");
        }
    }

    private async ValueTask DisconnectAsync()
    {
        logger.LogInformation("Disconnecting from {Uri}", uri);

        if (_ws.State == WebSocketState.Open)
        {
            await _ws.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }

        _ws.Dispose();
    }

    public Task SendAsync(string message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var buffer = new ArraySegment<byte>(bytes);
        return _ws.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
    }


    private static async Task<string> OnReceivedAsync(Stream inputStream)
    {
        var reader = new StreamReader(inputStream);
        var message = await reader.ReadToEndAsync();
        await inputStream.DisposeAsync();
        return message;
    }

    private static async Task<MemoryStream> ReceiveAsync(WebSocket ws, int bufferSize, CancellationToken ct)
    {
        var buffer = new byte[bufferSize];
        var outputStream = new MemoryStream(bufferSize);

        while (!ct.IsCancellationRequested)
        {
            var receiveResult = await ws.ReceiveAsync(buffer, ct);

            if (receiveResult.MessageType == WebSocketMessageType.Close)
            {
                return outputStream;
            }

            outputStream.Write(buffer, 0, receiveResult.Count);

            if (receiveResult.EndOfMessage)
            {
                break;
            }
        }

        outputStream.Position = 0;
        return outputStream;
    }

    public ValueTask DisposeAsync()
    {
        return DisconnectAsync();
    }
}