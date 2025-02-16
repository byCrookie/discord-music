using System.Text;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class HelpAction(
    Replier replier,
    ILogger<SeekAction> logger,
    IServiceProvider serviceProvider,
    IOptions<DiscordOptions> options
) : IDiscordAction
{
    public string Long => "help";
    public string Short => "h";

    public string Help =>
        """
        Show the help menu for the bot
        Usage: `help`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        var help = new StringBuilder();

        var actions = new List<Type>
        {
            typeof(JoinAction),
            typeof(LeaveAction),
            typeof(PlayAction),
            typeof(PlayNextAction),
            typeof(PauseAction),
            typeof(ResumeAction),
            typeof(NowPlayingAction),
            typeof(QueueAction),
            typeof(QueueClearAction),
            typeof(SkipAction),
            typeof(SeekAction),
            typeof(SeekForwardAction),
            typeof(SeekBackwardAction),
            typeof(ShuffleAction),
            typeof(LyricsAction),
            typeof(PingAction),
            typeof(AudioBarAction),
            typeof(HelpAction)
        };

        var actionInstances = serviceProvider.GetServices<IDiscordAction>().OrderBy(x => actions.IndexOf(x.GetType()));

        help.AppendLine(
            $"All actions must be prefixed with `{options.Value.Prefix}` (e.g. `{options.Value.Prefix}play` or `{options.Value.Prefix}p`)"
        );

        foreach (var action in actionInstances)
        {
            help.AppendLine();
            help.AppendLine($"-- `{action.Long}` / `{action.Short}` --");
            help.AppendLine(action.Help);
        }

        logger.LogTrace("Help");
        await replier
            .DirectMessage(message)
            .WithTitle("Help")
            .WithContent(help.ToString())
            .SendAsync(ct);
        
        return Result.Success;
    }
}
