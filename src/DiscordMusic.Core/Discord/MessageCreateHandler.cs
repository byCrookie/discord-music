using System.Diagnostics;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord;

public class MessageCreateHandler(
    ILogger<MessageCreateHandler> logger,
    IEnumerable<IDiscordAction> actions,
    IOptions<DiscordOptions> options,
    Replier replier,
    Cancellation cancellation,
    RestClient restClient
) : IMessageCreateGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        var ct = cancellation.CancellationToken;

        try
        {
            logger.LogTrace(
                "Message {Message} created by {User}",
                message.Content,
                message.Author.Username
            );

            if (
                !message.Content.StartsWith(
                    options.Value.Prefix,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                logger.LogTrace(
                    "Message {Message} does not start with command prefix {Prefix}",
                    message.Content,
                    options.Value.Prefix
                );
                return;
            }

            var allowed = IsChannelAllowed(message);

            if (allowed.IsError)
            {
                await replier.Reply().To(message).SendErrorAsync(allowed.ToPrint(), ct);
                return;
            }

            var roles = await IsRoleAllowed(message, ct);

            if (roles.IsError)
            {
                await replier.Reply().To(message).SendErrorAsync(roles.ToPrint(), ct);
                return;
            }

            var eval = EvaluateAction(message);

            if (eval.IsError)
            {
                await replier.Reply().To(message).SendErrorAsync(eval.ToPrint(), ct);
                return;
            }

            var (action, args) = eval.Value;
            logger.LogDebug(
                "Executing action {Action} for message {Message}",
                action.GetType().Name,
                message.Content
            );

            var execution = await action.ExecuteAsync(message, args, ct);

            if (execution.IsError)
            {
                logger.LogError(
                    "Failed to execute action {Action} for message {Message}: {Error}",
                    action.GetType().Name,
                    message.Content,
                    execution.ToPrint()
                );
                await replier.Reply().To(message).SendErrorAsync(execution.ToPrint(), ct);
                return;
            }

            logger.LogTrace(
                "Executed action {Action} for message {Message}",
                action.GetType().Name,
                message.Content
            );

            using var proc = Process.GetCurrentProcess();
            logger.LogInformation("Memory usage: {MemoryUsage}", proc.PrivateMemorySize64.Bytes());
        }
        catch (Exception e)
        {
            logger.LogError(
                e,
                "Failed to handle message {Message} from {User}",
                message.Content,
                message.Author.Username
            );
            await replier.Reply().To(message).SendErrorAsync(e.Message, ct);
        }
    }

    private async Task<ErrorOr<Success>> IsRoleAllowed(Message message, CancellationToken ct)
    {
        if (options.Value.Roles.Count == 0)
        {
            return Result.Success;
        }

        if (message.GuildId is null)
        {
            logger.LogError("Failed to get guild id for message {Message}", message.Content);
            return Error.Forbidden(description: "You can't use this command. Not a guild message.");
        }

        var guild = await restClient.GetGuildAsync(message.GuildId.Value, cancellationToken: ct);

        var matchingRoles = guild
            .Roles.Where(gr => options.Value.Roles.Any(r => r == gr.Value.Name))
            .ToList();

        if (matchingRoles.Count == 0)
        {
            logger.LogError(
                "No rules configured ({RolesConfigured}) match any roles in the guild by name ({RolesInGuild})",
                string.Join(",", options.Value.Roles),
                string.Join(",", message.Guild?.Roles.Select(r => r.Value.Name).ToList() ?? [])
            );
            return Error.Forbidden(
                description: $"You can't use this command. Valid roles are not configured on the server - {string.Join("|", options.Value.Roles)}."
            );
        }

        var author = await guild.GetUserAsync(message.Author.Id, cancellationToken: ct);

        var matchingUserRoles = matchingRoles.Where(mr => author.RoleIds.Contains(mr.Key)).ToList();

        if (matchingUserRoles.Count != 0)
        {
            return Result.Success;
        }

        logger.LogError(
            "User {User} does not have any of the roles {Roles}",
            message.Author.Username,
            string.Join("|", matchingRoles)
        );
        return Error.Forbidden(
            description: $"You can't use this command. You don't have any of these roles - {string.Join("|", options.Value.Roles)}."
        );
    }

    private ErrorOr<Success> IsChannelAllowed(Message message)
    {
        if (options.Value.Allow.Count == 0 && options.Value.Deny.Count == 0)
        {
            return Result.Success;
        }

        var channel = message.Guild?.Channels.SingleOrDefault(c => c.Key == message.ChannelId);

        if (channel is null)
        {
            logger.LogError("Failed to get guild channel for id {ChannelId}", message.ChannelId);
            return Error.Conflict(
                description: "You can't use this command in this channel. Not a guild channel."
            );
        }

        if (options.Value.Deny.Contains(channel.Value.Value.Name))
        {
            logger.LogTrace("Channel {Channel} is in deny list", channel.Value.Value.Name);
            return Error.Forbidden(
                description: "You can't use this command in this channel. Channel is not allowed."
            );
        }

        if (
            options.Value.Allow.Count == 0
            || options.Value.Allow.Contains(channel.Value.Value.Name)
        )
        {
            return Result.Success;
        }

        logger.LogTrace("Channel {Channel} is not in allow list", channel.Value.Value.Name);
        return Error.Forbidden(
            description: "You can't use this command in this channel. Channel is not allowed."
        );
    }

    private ErrorOr<(IDiscordAction Action, string[] Args)> EvaluateAction(Message message)
    {
        var parts = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0][options.Value.Prefix.Length..];
        var args = parts.Skip(1).ToArray();

        var matchingActions = actions.Where(a => a.Long == command || a.Short == command).ToList();

        switch (matchingActions.Count)
        {
            case 0:
                logger.LogError("No action found for message {Message}", message.Content);
                return Error.Conflict(description: $"No action found for {message.Content}");
            case > 1:
                logger.LogError(
                    "Multiple actions {Actions} found for message {Message}",
                    string.Join(", ", matchingActions.Select(a => a.GetType().Name)),
                    message.Content
                );
                return Error.Conflict(description: $"Multiple actions found for {message.Content}");
        }

        var action = matchingActions.Single();
        logger.LogTrace(
            "Action {Action} found for message {Message} with args {Args}",
            action.GetType().Name,
            message.Content,
            string.Join(" ", args)
        );
        return (action, args);
    }
}
