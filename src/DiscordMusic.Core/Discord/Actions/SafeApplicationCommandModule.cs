using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

internal abstract class SafeApplicationCommandModule
    : ApplicationCommandModule<ApplicationCommandContext>
{
    protected async Task SafeRespondAsync(
        InteractionCallbackProperties callback,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        try
        {
            await RespondAsync(callback, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown/dispose.
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to respond to interaction. GuildId={GuildId} ChannelId={ChannelId} UserId={UserId}",
                Context.Guild?.Id,
                Context.Channel?.Id,
                Context.User.Id
            );
        }
    }

    protected async Task SafeModifyResponseAsync(
        Action<MessageOptions> modify,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        try
        {
            await ModifyResponseAsync(modify, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown/dispose.
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to modify interaction response. GuildId={GuildId} ChannelId={ChannelId} UserId={UserId}",
                Context.Guild?.Id,
                Context.Channel?.Id,
                Context.User.Id
            );
        }
    }
}
