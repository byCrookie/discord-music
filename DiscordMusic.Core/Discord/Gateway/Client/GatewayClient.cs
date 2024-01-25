using System.Text.Json;
using System.Text.Json.Serialization;
using DiscordMusic.Core.Discord.Gateway.Events;
using DiscordMusic.Core.Utils;
using DiscordMusic.Core.Websockets;
using Flurl;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Gateway.Client;

public class GatewayClient(
    IGatewayService gatewayService,
    ILogger<GatewayClient> logger,
    ILogger<WebSocketClient> webSocketLogger) : IGatewayClient
{
    private WebSocketClient? _ws;
    private readonly Dictionary<OpCodes, List<Handler>> _opCodeHandlers = new();
    private readonly Dictionary<DiscordEvent, List<Handler>> _eventHandlers = new();

    public int? LastSequenceNumber { get; private set; }

    public async Task RunAsync(CancellationToken ct)
    {
        if (_ws is not null)
        {
            throw new Exception("Already connected");
        }

        logger.LogDebug("Getting gateway bot url");
        var gateway = await gatewayService.GetGatewayBotAsync(ct);

        var gatewayUrl = gateway.Url.SetQueryParams(new { v = 10, encoding = "json" });
        logger.LogInformation("Connecting to {GatewayUrl}", gatewayUrl);

        _ws = new WebSocketClient(gatewayUrl.ToUri(), webSocketLogger);

        await foreach (var message in _ws.ConnectAsync(ct))
        {
            await OnAsync(message, ct);
        }

        await _ws.DisposeAsync();
    }

    public Task SendAsync<T>(T data, CancellationToken ct)
    {
        if (_ws is null)
        {
            throw new Exception("Not connected");
        }

        var json = JsonSerializer.Serialize(data,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        logger.LogDebug("Sending {Data}", json);
        return _ws.SendAsync(json, ct);
    }

    public void On<T>(OpCodes opCode, Func<GatewayEvent, CancellationToken, Task> handler) where T : GatewayEvent
    {
        logger.LogDebug("Registering handler for {OpCode}", opCode);

        if (_opCodeHandlers.TryGetValue(opCode, out var handlers))
        {
            handlers.Add(new Handler(handler, typeof(T)));
            return;
        }

        _opCodeHandlers.Add(opCode, [new Handler(handler, typeof(T))]);
    }

    public void On<T>(DiscordEvent discordEvent, Func<GatewayEvent, CancellationToken, Task> handler)
        where T : GatewayEvent
    {
        logger.LogDebug("Registering handler for {DiscordEvent}", discordEvent);

        if (_eventHandlers.TryGetValue(discordEvent, out var handlers))
        {
            handlers.Add(new Handler(handler, typeof(T)));
            return;
        }

        _eventHandlers.Add(discordEvent, [new Handler(handler, typeof(T))]);
    }

    private async Task OnAsync(string data, CancellationToken ct)
    {
        logger.LogDebug("Received {Data}", data);

        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        var gatewayEvent = JsonSerializer.Deserialize<GatewayEvent>(data);

        if (gatewayEvent is null)
        {
            throw new InvalidOperationException("Gateway event is null");
        }

        if (gatewayEvent.OpCode == OpCodes.Dispatch)
        {
            LastSequenceNumber = gatewayEvent.SequenceNumber;
            
            foreach (var handler in _eventHandlers
                         .Where(h => h.Key.GetEnumMemberValue() == gatewayEvent.EventName)
                         .SelectMany(h => h.Value))
            {
                var parsedEvent = JsonSerializer.Deserialize(data, handler.Type)!;
                await handler.HandlerFunc((GatewayEvent)parsedEvent, ct);
            }
            
            return;
        }

        foreach (var handler in _opCodeHandlers.Where(h => h.Key == gatewayEvent.OpCode).SelectMany(h => h.Value))
        {
            var parsedEvent = JsonSerializer.Deserialize(data, handler.Type)!;
            await handler.HandlerFunc((GatewayEvent)parsedEvent, ct);
        }
    }
}

internal record Handler(Func<GatewayEvent, CancellationToken, Task> HandlerFunc, Type Type);