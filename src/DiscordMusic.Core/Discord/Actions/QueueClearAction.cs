using DiscordMusic.Core.Discord.Voice;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class QueueClearAction(IVoiceHost voiceHost, Replier replier, ILogger<QueueClearAction> logger) : IDiscordAction
{
    public string Long => "clear";

    public string Short => "c";

    public string Help =>
        """
            Clear the queue
            Usage: `clear`
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Queue clear");
        var clear = await voiceHost.QueueClearAsync(message, ct);

        if (clear.IsError)
        {
            return clear.Errors;
        }
        
        await replier
            .Reply()
            .To(message)
            .WithEmbed("Cleared", "The queue has been cleared")
            .WithDeletion()
            .SendAsync(ct);
        
        return Result.Success;
    }
}
