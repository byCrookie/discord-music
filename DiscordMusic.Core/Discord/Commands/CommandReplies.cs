using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

public static class CommandReplies
{
    public static async Task OkAsync(
        ICommandContext context,
        ILogger logger,
        string title,
        string publicMessage,
        [StructuredMessageTemplate] string? message,
        params object?[] args)
    {
        logger.LogTrace(message, args);

        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(publicMessage)
            .WithColor(Color.Blue)
            .Build();

        await context
            .Channel
            .SendMessageAsync(
                embed: embed,
                messageReference: new MessageReference(
                    context.Message.Id,
                    context.Channel.Id,
                    context.Guild.Id
                ));
    }

    public static async Task ErrorAsync(
        ICommandContext context,
        ILogger logger,
        string publicError,
        [StructuredMessageTemplate] string? message,
        params object?[] args)
    {
        logger.LogTrace(message, args);

        var embed = new EmbedBuilder()
            .WithTitle("Error")
            .WithDescription(publicError)
            .WithColor(Color.Red)
            .Build();

        var response = await context
            .Channel
            .SendMessageAsync(
                embed: embed,
                messageReference: new MessageReference(
                    context.Message.Id,
                    context.Channel.Id,
                    context.Guild.Id
                ));

        logger.LogTrace("Waiting 10 seconds before deleting message {MessageId} from {ChannelId}...", response.Id,
            context.Channel.Id);
        await Task.Delay(TimeSpan.FromSeconds(10));
        logger.LogTrace("Deleting message {MessageId} from {ChannelId}...", response.Id, context.Channel.Id);
        await response.DeleteAsync();
    }
}
