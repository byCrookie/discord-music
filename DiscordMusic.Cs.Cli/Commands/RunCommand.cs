using Cocona;
using Cocona.Application;
using CSGSI;
using CSGSI.Events;
using CSGSI.Nodes;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordMusic.Core.Errors;
using DiscordMusic.Core.Global;
using DiscordMusic.Core.Utils;
using DiscordMusic.Cs.Cli.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DiscordMusic.Cs.Cli.Commands;

internal class RunCommand(
    [FromService] ICoconaAppContextAccessor contextAccessor,
    IOptions<DiscordOptions> discordOptions,
    IOptions<CsOptions> csOptions,
    ILogger<RunCommand> logger,
    ILogger<DiscordSocketClient> clientLogger,
    DiscordRestClient client)
{
    private bool _isPaused;

    [UsedImplicitly]
    [ExceptionFilter]
    [Command("run")]
    public async Task RunAsync(
        GlobalArguments globalArguments,
        [Option('u',
            Description =
                "Uri to listen to. Use on linux like http://0.0.0.0:3000 and on windows like http://localhost:3000.")]
        string uri)
    {
        var ct = contextAccessor.Current?.CancellationToken ?? CancellationToken.None;

        client.Log += logMessage => LogAsync(clientLogger, logMessage);

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5, BackoffType = DelayBackoffType.Linear, Delay = TimeSpan.FromSeconds(20)
            })
            .Build();

        await pipeline.ExecuteAsync(async _ =>
        {
            try
            {
                logger.LogInformation("Logging in to Discord");
                await client.LoginAsync(TokenType.Bot, discordOptions.Value.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to login to Discord");
                throw;
            }
        }, ct);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(uri);
        builder.Services.AddSerilog();
        var app = builder.Build();

        app.MapPost("/", async (HttpRequest request) =>
        {
            var body = await new StreamReader(request.Body).ReadToEndAsync(ct);
            logger.LogTrace("Received body: {Body}", body);

            Task.Run(async () =>
            {
                var gameState = new GameState(body);

                if (csOptions.Value.Whitelist.Count != 0 &&
                    !csOptions.Value.Whitelist.Contains(gameState.Auth.Token))
                {
                    logger.LogWarning("Forbidden token {Token}. Not whitelisted.", gameState.Auth.Token);
                    return;
                }

                if (gameState.Previously.Round.Phase != gameState.Round.Phase)
                {
                    await OnNewGameStateAsync(new RoundPhaseChangedEventArgs(gameState));
                }
            }, ct).FireAndForget();

            return Results.Ok();
        });

        await app.RunAsync();
        logger.LogInformation("Logout from Discord");
        await client.LogoutAsync();
    }

    private Task OnNewGameStateAsync(RoundPhaseChangedEventArgs args)
    {
        if (args.CurrentPhase == RoundPhase.Undefined)
        {
            logger.LogInformation("Round phase: {Phase}", args.CurrentPhase);
            return Task.CompletedTask;
        }

        logger.LogInformation("Round phase: {Phase}", args.CurrentPhase);

        switch (_isPaused)
        {
            case true when args.CurrentPhase is RoundPhase.FreezeTime or RoundPhase.Over:
                _isPaused = false;
                return SendAsync();
            case false when args.CurrentPhase == RoundPhase.Live:
                _isPaused = true;
                return SendAsync();
        }

        return Task.CompletedTask;
    }

    private async Task SendAsync()
    {
        var guild = await client.GetGuildAsync(discordOptions.Value.GuildId);
        var channel = await guild.GetChannelAsync(discordOptions.Value.ChannelId);

        if (channel is not IMessageChannel messageChannel)
        {
            logger.LogError("Channel {ChannelId} is not a message channel", discordOptions.Value.ChannelId);
            throw new ArgumentException($"Channel {discordOptions.Value.ChannelId} is not a message channel");
        }

        // var connections = await client.GetConnectionsAsync();
        // if (connections.Count == 0)
        // {
        //     logger.LogWarning("Bot is not connected to any voice channel");
        //     return;
        // }

        logger.LogInformation("Sending message {Message} to {ChannelId}...", discordOptions.Value.Message,
            discordOptions.Value.ChannelId);
        var message = await messageChannel.SendMessageAsync(discordOptions.Value.Message);
        logger.LogInformation("Deleting message {MessageId} from {ChannelId}...", message.Id,
            discordOptions.Value.ChannelId);
        await message.DeleteAsync();
    }

    private static Task LogAsync(ILogger logger, LogMessage logMessage)
    {
        var logLevel = logMessage.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        logger.Log(logLevel, logMessage.Exception, "{Message}", logMessage.Message);
        return Task.CompletedTask;
    }
}
