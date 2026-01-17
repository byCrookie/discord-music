using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord;

public class RequireRoleDjAttribute<TContext> : PreconditionAttribute<TContext>
    where TContext : ApplicationCommandContext
{
    public override async ValueTask<PreconditionResult> EnsureCanExecuteAsync(
        TContext context,
        IServiceProvider? serviceProvider
    )
    {
        var logger = serviceProvider?.GetService<ILogger<RequireRoleDjAttribute<TContext>>>();

        if (logger is null)
        {
            return PreconditionResult.Fail("Logger service is not available.");
        }

        if (context.Guild is null)
        {
            logger.LogError("Guild is null");
            return PreconditionResult.Fail("This command can only be used in a guild.");
        }

        var djRole = context.Guild.Roles.Values.SingleOrDefault(gr =>
            gr.Name.Equals("DJ", StringComparison.InvariantCultureIgnoreCase)
        );

        if (djRole is null)
        {
            logger.LogError(
                "Role with name 'DJ' (case-insensitive) is not configured on the server {Guild}",
                context.Guild.Name
            );
            return PreconditionResult.Fail(
                "Role with name 'DJ' (case-insensitive) is not configured on this server."
            );
        }

        var user = await context.Guild.GetUserAsync(context.User.Id);

        if (user.RoleIds.Contains(djRole.Id))
        {
            return PreconditionResult.Success;
        }

        logger.LogError(
            "User {User} does not have required role 'DJ' (case-insensitive) on server {Guild}",
            context.User.Username,
            context.Guild.Name
        );

        return PreconditionResult.Fail(
            "You do not have the required 'DJ' (case-insensitive) role to use this command."
        );
    }
}
