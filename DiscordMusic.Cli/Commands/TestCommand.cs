using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Cocona;
using Cocona.Application;
using DiscordMusic.Cli.Commands.Global;
using DiscordMusic.Core.Discord.Gateway.Client;
using DiscordMusic.Core.Discord.Gateway.Events;
using DiscordMusic.Core.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cli.Commands;

public class TestCommand(
    [FromService] ICoconaAppContextAccessor contextAccessor,
    IOptions<DiscordSecrets> discordSecrets,
    IGatewayClient gatewayClient,
    ILogger<TestCommand> logger)
{
    [UsedImplicitly]
    [Command("test")]
    public Task TestAsync(GlobalArguments globalArguments)
    {
        var cancellationToken = contextAccessor.Current?.CancellationToken ?? CancellationToken.None;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var heartbeatAckReceived = true;

        gatewayClient.On<GatewayEvent<HelloEvent>>(OpCodes.Hello, async (hello, ct) =>
        {
            var helloEvent = (GatewayEvent<HelloEvent>)hello;
            logger.LogInformation("Hello");

            await Task.Factory.StartNew(async () =>
            {
                var jitter = helloEvent.Data!.HeartbeatInterval * Random.Shared.NextDouble();
                logger.LogDebug("Jitter: {Jitter}", jitter);

                await Task.Delay(TimeSpan.FromMilliseconds(jitter), ct);

                while (!ct.IsCancellationRequested)
                {
                    if (!heartbeatAckReceived)
                    {
                        logger.LogWarning("Heartbeat Ack not received");
                        await cts.CancelAsync();
                        break;
                    }

                    await SendHeartbeatAsync(ct);
                    await Task.Delay(TimeSpan.FromMilliseconds(helloEvent.Data!.HeartbeatInterval), ct);
                }
            }, ct);

            await gatewayClient.SendAsync(new GatewayEvent<IdentifyEvent>
            {
                OpCode = OpCodes.Identify,
                Data = new IdentifyEvent(
                    discordSecrets.Value.Token,
                    new IdentifyEventProperties(
                        GetOs(),
                        "DiscordMusic",
                        "DiscordMusic"
                    ),
                    false,
                    null,
                    null,
                    null,
                    GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildVoiceStates |
                    GatewayIntents.GuildMembers | GatewayIntents.MessageContent | GatewayIntents.GuildPresences
                )
            }, ct);
        });

        gatewayClient.On<GatewayEvent<HeartbeatEvent>>(OpCodes.Heartbeat, (_, ct) =>
        {
            logger.LogDebug("Heartbeat");
            return SendHeartbeatAsync(ct);
        });

        gatewayClient.On<GatewayEvent<HeartbeatAckEvent>>(OpCodes.HeartbeatAck, (_, _) =>
        {
            heartbeatAckReceived = true;
            logger.LogDebug("Heartbeat Ack");
            return Task.CompletedTask;
        });

        gatewayClient.On<GatewayEvent<ReadyEvent>>(DiscordEvent.Ready, (ready, _) =>
        {
            var readyEvent = (GatewayEvent<ReadyEvent>)ready;
            logger.LogInformation("Ready");
            return Task.CompletedTask;
        });

        gatewayClient.On<GatewayEvent<Integration>>(DiscordEvent.IntegrationCreate, async (integration, ct) =>
        {
            var integrationEvent = (GatewayEvent<Integration>)integration;
            logger.LogInformation("interaction");

            var messageContent = "";

            if (string.IsNullOrWhiteSpace(messageContent) || !messageContent.Trim().StartsWith('!'))
            {
                return;
            }

            var regex = new Regex("!(\\w+)( .+)?", RegexOptions.Compiled);
            var match = regex.Match(messageContent);

            if (!match.Success)
            {
                return;
            }

            var command = match.Groups[1].Value;
            var arguments = match.Groups[2].Value;

            logger.LogInformation("Command: {Command}", command);

            switch (command)
            {
                case "play":
                    logger.LogInformation("Arguments: {Arguments}", arguments);
                    break;
                case "help":
                    logger.LogInformation("Arguments: {Arguments}", arguments);
                    break;
                case "join":
                    await gatewayClient.SendAsync(new GatewayEvent<VoiceStateUpdate>
                    {
                        OpCode = OpCodes.VoiceStateUpdate,
                        Data = new VoiceStateUpdate
                        {
                            // GuildId = interaction.Data!.Member!.GuildId,
                            // ChannelId = interaction.Data!.Member.ChannelId,
                            SelfDeaf = false,
                            SelfMute = false
                        }
                    }, ct);
                    break;
            }
        });

        return gatewayClient.RunAsync(cts.Token);
    }

    private static string GetOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "MacOS";
        }

        throw new Exception("Unknown OS");
    }

    private Task SendHeartbeatAsync(CancellationToken ct)
    {
        return gatewayClient.SendAsync(new GatewayEvent<int?>
        {
            OpCode = OpCodes.Heartbeat,
            Data = gatewayClient.LastSequenceNumber
        }, ct);
    }
}