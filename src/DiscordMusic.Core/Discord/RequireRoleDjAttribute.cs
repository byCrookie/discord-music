using System.Diagnostics;
using DiscordMusic.Core.Observability;
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
        using var activity = DiscordMusicObservability.StartDiscordPreconditionActivity(
            "require_role_dj",
            context.Guild?.Id,
            context.User.Id
        );
        try
        {
            var logger = serviceProvider?.GetService<ILogger<RequireRoleDjAttribute<TContext>>>();

            if (logger is null)
            {
                Record(context, "missing_logger", activity);
                return PreconditionResult.Fail("Logger service is not available.");
            }

            if (context.Guild is null)
            {
                logger.LogError("Guild is null");
                Record(context, "missing_guild", activity);
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
                Record(context, "missing_role", activity);
                return PreconditionResult.Fail(
                    "Role with name 'DJ' (case-insensitive) is not configured on this server."
                );
            }

            var user = await context.Guild.GetUserAsync(context.User.Id);

            if (user.RoleIds.Contains(djRole.Id))
            {
                Record(context, "accepted", activity);
                return PreconditionResult.Success;
            }

            logger.LogError(
                "User {User} does not have required role 'DJ' (case-insensitive) on server {Guild}",
                context.User.Username,
                context.Guild.Name
            );

            Record(context, "missing_user_role", activity);
            return PreconditionResult.Fail(
                "You do not have the required 'DJ' (case-insensitive) role to use this command."
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static void Record(TContext context, string result, Activity? activity)
    {
        DiscordMusicObservability.RecordDiscordPrecondition(
            "require_role_dj",
            context.Guild?.Id,
            context.User.Id,
            result,
            activity
        );
    }
}
