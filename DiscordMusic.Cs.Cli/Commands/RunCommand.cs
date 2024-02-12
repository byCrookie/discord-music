using Cocona;
using Cocona.Application;
using CSGSI;
using CSGSI.Events;
using CSGSI.Nodes;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordMusic.Cs.Cli.Commands.Global;
using DiscordMusic.Cs.Cli.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Commands;

internal class RunCommand(
    [FromService] ICoconaAppContextAccessor contextAccessor,
    IOptions<DiscordOptions> discordOptions,
    ILogger<RunCommand> logger,
    ILogger<DiscordSocketClient> clientLogger,
    DiscordRestClient client)
{
    private bool _isPaused;

    [UsedImplicitly]
    [Command("run")]
    public async Task RunAsync(
        GlobalArguments globalArguments,
        [Option('u', Description = "Uri to listen to")]
        string uri = "http://localhost:3000")
    {
        var ct = contextAccessor.Current?.CancellationToken ?? CancellationToken.None;

        logger.LogInformation("Login in to Discord...");
        client.Log += logMessage => LogAsync(clientLogger, logMessage);
        await client.LoginAsync(TokenType.Bot, discordOptions.Value.Token);

        var gsl = new GameStateListener(uri);
        gsl.RoundPhaseChanged += args => OnNewGameStateAsync(args).Wait(ct);
        gsl.EnableRaisingIntricateEvents = true;

        try
        {
            gsl.Start();
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            gsl.Stop();
        }
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

        logger.LogInformation("Sending message {Message} to {ChannelId}...", discordOptions.Value.Message,
            discordOptions.Value.ChannelId);
        var message = await messageChannel.SendMessageAsync(discordOptions.Value.Message);
        await Task.Delay(TimeSpan.FromSeconds(1));
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