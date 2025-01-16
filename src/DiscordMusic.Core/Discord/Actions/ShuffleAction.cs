using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class ShuffleAction(IVoiceHost voiceHost, IReplies replies, ILogger<ShuffleAction> logger) : IDiscordAction
{
    public string Long => "shuffle";
    public string Short => "sh";

    public string Help =>
        """
        Shuffle the queue
        Usage: `shuffle`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Shuffle");
        var shuffle = await voiceHost.ShuffleAsync(message, ct);

        if (shuffle.IsError)
        {
            return shuffle.Errors;
        }

        if (shuffle.Value.Track is null)
        {
            await replies.SendWithDeletionAsync(
                message,
                "Shuffle",
                "The queue is empty",
                IReplies.DefaultDeletionDelay,
                ct
            );
            return Result.Success;
        }

        await replies.SendWithDeletionAsync(
            message,
            "Next",
            $"**{shuffle.Value.Track!.Name}** by **{shuffle.Value.Track!.Artists}** ({shuffle.Value.Track!.Duration.HummanizeSecond()})",
            IReplies.DefaultDeletionDelay,
            ct
        );

        return Result.Success;
    }
}
