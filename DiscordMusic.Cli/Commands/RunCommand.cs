using Cocona;
using Cocona.Application;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusic.Core.Discord.Commands;
using DiscordMusic.Core.Discord.Options;
using DiscordMusic.Shared.Errors;
using DiscordMusic.Shared.Global;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IResult = Discord.Commands.IResult;

namespace DiscordMusic.Cli.Commands;

internal class RunCommand(
    IServiceProvider serviceProvider,
    [FromService] ICoconaAppContextAccessor contextAccessor,
    IOptions<DiscordOptions> discordOptions,
    ILogger<RunCommand> logger,
    ILogger<DiscordSocketClient> clientLogger,
    ILogger<CommandService> commandLogger,
    DiscordSocketClient client,
    CommandService commands)
{
    [UsedImplicitly]
    [ExceptionFilter]
    [Cocona.Command("run")]
    public async Task RunAsync(GlobalArguments globalArguments)
    {
        var ct = contextAccessor.Current?.CancellationToken ?? CancellationToken.None;

        logger.LogInformation("Login in to Discord...");
        client.Log += logMessage => LogAsync(clientLogger, logMessage);
        await client.LoginAsync(TokenType.Bot, discordOptions.Value.Token);

        logger.LogInformation("Starting Discord client...");
        await client.StartAsync();
        commands.Log += logMessage => LogAsync(commandLogger, logMessage);

        commands.CommandExecuted += CommandExecuteAsync;
        client.MessageReceived += MessageReceivedAsync;

        await CommandRegistration.AddCommandsAsync(commands, serviceProvider);

        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task MessageReceivedAsync(IMessage rawMessage)
    {
        logger.LogInformation("Message: {Message}", rawMessage.Content);

        if (rawMessage is not SocketUserMessage message)
        {
            logger.LogTrace("Message is not a user message");
            return;
        }

        if (message.Source != MessageSource.User && message.Author.Id != client.CurrentUser.Id)
        {
            logger.LogTrace("Message is not a user message or is not from discord-music bot");
            return;
        }

        if (discordOptions.Value.Whitelist.Count == 0 &&
            !discordOptions.Value.Whitelist.Contains(rawMessage.Channel.Name))
        {
            logger.LogDebug("Channel {Channel} not in whitelist", rawMessage.Channel.Name);
            return;
        }

        if (discordOptions.Value.Blacklist.Contains(rawMessage.Channel.Name))
        {
            logger.LogDebug("Channel {Channel} in blacklist", rawMessage.Channel.Name);
            return;
        }

        var argPos = 0;
        if (!message.HasStringPrefix(discordOptions.Value.Prefix, ref argPos) &&
            !message.HasMentionPrefix(client.CurrentUser, ref argPos))
        {
            logger.LogDebug("Message does not have prefix or mention");
            return;
        }

        await commands.ExecuteAsync(new CommandContext(client, message), argPos, serviceProvider);
    }

    private Task CommandExecuteAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        if (!command.IsSpecified)
        {
            return Task.CompletedTask;
        }

        if (result.IsSuccess)
        {
            logger.LogTrace("Command executed: {Command}", command.Value.Name);
            return Task.CompletedTask;
        }

        return CommandReplies.ErrorAsync(
            context,
            logger,
            $"{result}",
            "Command {Command} failed with error {Error}",
            command.Value.Name,
            $"{result}"
        );
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
