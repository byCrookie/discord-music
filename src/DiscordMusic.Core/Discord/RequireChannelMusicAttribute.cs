using System.Diagnostics;
using DiscordMusic.Core.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord;

public class RequireChannelMusicAttribute<TContext> : PreconditionAttribute<TContext>
    where TContext : ApplicationCommandContext
{
    public override async ValueTask<PreconditionResult> EnsureCanExecuteAsync(
        TContext context,
        IServiceProvider? serviceProvider
    )
    {
        using var activity = DiscordMusicObservability.StartDiscordPreconditionActivity(
            "require_channel_music",
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

            var channels = await context.Guild.GetChannelsAsync();

            var musicChannel = channels.SingleOrDefault(c =>
                c.Name.Equals("music", StringComparison.InvariantCultureIgnoreCase)
            );

            if (musicChannel is not null && context.Channel.Id == musicChannel.Id)
            {
                Record(context, "accepted", activity);
                return PreconditionResult.Success;
            }

            logger.LogError("Not in channel with name 'music' (case-insensitive).");
            Record(context, "wrong_channel", activity);
            return PreconditionResult.Fail("Not in channel with name 'music' (case-insensitive).");
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
            "require_channel_music",
            context.Guild?.Id,
            context.User.Id,
            result,
            activity
        );
    }
}
