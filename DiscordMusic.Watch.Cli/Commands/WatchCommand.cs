using System.IO.Abstractions;
using System.Text.Json;
using Cocona;
using Cocona.Application;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordMusic.Watch.Cli.Commands.Global;
using DiscordMusic.Watch.Cli.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Watch.Cli.Commands;

internal class WatchCommand(
    [FromService] ICoconaAppContextAccessor contextAccessor,
    IOptions<DiscordWatchOptions> discordOptions,
    ILogger<WatchCommand> logger,
    ILogger<DiscordSocketClient> clientLogger,
    DiscordRestClient client,
    IFileSystem fileSystem)
{
    [UsedImplicitly]
    [Command("watch")]
    public async Task WatchAsync(GlobalArguments globalArguments)
    {
        var ct = contextAccessor.Current?.CancellationToken ?? CancellationToken.None;

        logger.LogInformation("Login in to Discord...");
        client.Log += logMessage => LogAsync(clientLogger, logMessage);
        await client.LoginAsync(TokenType.Bot, discordOptions.Value.Token);

        await WatchAsync(ct);
    }

    private Task WatchAsync(CancellationToken ct)
    {
        logger.LogInformation("Watching file {Watch} for changes...", discordOptions.Value.Watch);
        
        var watchFile = fileSystem.FileInfo.New(discordOptions.Value.Watch);
        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        
        Task.Run(async () =>
        {
            while (await periodicTimer.WaitForNextTickAsync(ct))
            {
                logger.LogTrace("Refreshing file {Watch}...", discordOptions.Value.Watch);
                watchFile.Refresh();
            }
        }, ct);

        var fileWatcher = new FileSystemWatcher
        {
            Path = watchFile.DirectoryName!,
            NotifyFilter = NotifyFilters.LastWrite
                           | NotifyFilters.Size
                           | NotifyFilters.LastAccess
                           | NotifyFilters.CreationTime
                           | NotifyFilters.Attributes
                           | NotifyFilters.Security,
            Filter = watchFile.Name
        };

        var fileStream = fileSystem.File.Open(discordOptions.Value.Watch, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite);
        var streamReader = new StreamReader(fileStream);
        _ = streamReader.ReadToEndAsync(ct);

        fileWatcher.Changed += async (_, args) =>
        {
            logger.LogTrace("File {Watch} changed - {Args}", discordOptions.Value.Watch,
                JsonSerializer.Serialize(args));
            var guild = await client.GetGuildAsync(discordOptions.Value.GuildId);
            var channel = await guild.GetChannelAsync(discordOptions.Value.ChannelId);

            if (channel is not IMessageChannel messageChannel)
            {
                logger.LogError("Channel {ChannelId} is not a message channel", discordOptions.Value.ChannelId);
                throw new ArgumentException($"Channel {discordOptions.Value.ChannelId} is not a message channel");
            }

            var content = await streamReader.ReadToEndAsync(ct);
            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            if (!lines.Any(line => line.Contains(discordOptions.Value.Message, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogTrace("No {Command} found in change", discordOptions.Value.Message);
                return;
            }

            logger.LogInformation("Sending message {Message} to {ChannelId}...", discordOptions.Value.Message,
                discordOptions.Value.ChannelId);
            await messageChannel.SendMessageAsync(discordOptions.Value.Message);
        };

        fileWatcher.EnableRaisingEvents = true;

        return Task.Delay(Timeout.Infinite, ct);
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