using DiscordMusic.Core.Discord.Gateway.Events;

namespace DiscordMusic.Core.Discord.Gateway.Client;

public interface IGatewayClient
{
    Task RunAsync(CancellationToken ct);
    Task SendAsync<T>(T data, CancellationToken ct);
    void On<T>(OpCodes opCode, Func<GatewayEvent, CancellationToken, Task> handler) where T : GatewayEvent;
    void On<T>(DiscordEvent discordEvent, Func<GatewayEvent, CancellationToken, Task> handler) where T : GatewayEvent;
    int? LastSequenceNumber { get; }
}