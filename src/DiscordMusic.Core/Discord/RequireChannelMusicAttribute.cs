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

        var channels = await context.Guild.GetChannelsAsync();

        var musicChannel = channels.SingleOrDefault(c =>
            c.Name.Equals("music", StringComparison.InvariantCultureIgnoreCase)
        );

        if (musicChannel is not null && context.Channel.Id == musicChannel.Id)
        {
            return PreconditionResult.Success;
        }

        logger.LogError("Not in channel with name 'music' (case-insensitive).");
        return PreconditionResult.Fail("Not in channel with name 'music' (case-insensitive).");
    }
}
