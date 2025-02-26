using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class PingAction(Replier replier, ILogger<SeekAction> logger) : IDiscordAction
{
    public string Long => "ping";
    public string Short => "pi";

    public string Help =>
        """
            Ping the bot. It will pong back.
            Usage: `ping`
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Ping");
        await replier.Reply().To(message).WithEmbed("Pong", "You pinged me!").WithDeletion().SendAsync(ct);
        return Result.Success;
    }
}
