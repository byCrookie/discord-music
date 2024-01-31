using Discord;
using Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

public static class CommandReplies
{
    public static async Task OkAsync<T>(
        ICommandContext context,
        ILogger<T> logger,
        string title,
        string publicMessage,
        [StructuredMessageTemplate] string? message,
        params object?[] args)
        where T : class
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

    public static async Task ErrorAsync<T>(
        ICommandContext context,
        ILogger<T> logger,
        string publicError,
        [StructuredMessageTemplate] string? message,
        params object?[] args)
        where T : class
    {
        logger.LogTrace(message, args);

        var embed = new EmbedBuilder()
            .WithTitle("Error")
            .WithDescription(publicError)
            .WithColor(Color.Red)
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
}