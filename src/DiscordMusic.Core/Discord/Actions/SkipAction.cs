using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class SkipAction(IVoiceHost voiceHost, Replier replier, ILogger<SkipAction> logger) : IDiscordAction
{
    public string Long => "skip";

    public string Short => "s";

    public string Help =>
        """
            Skip to a specific track in the queue. If the position is not provided, the next track will be played.
            Usage: `skip <position>` | `skip`
            <position> - The position of the track to skip to
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Skip");

        var skipCount = 0;

        if (args.Length != 0)
        {
            if (!int.TryParse(args[0], out skipCount) || skipCount < 1)
            {
                await replier.Reply().To(message).WithEmbed("Skip", "Invalid position").WithDeletion().SendAsync(ct);

                return Result.Success;
            }

            skipCount--;
        }

        var skip = await voiceHost.SkipAsync(message, skipCount, ct);

        if (skip.IsError)
        {
            return skip.Errors;
        }

        if (skip.Value.Track is null)
        {
            await replier
                .Reply()
                .To(message)
                .WithEmbed("Skip", "Queue is empty. Can not skip.")
                .WithDeletion()
                .SendAsync(ct);

            return Result.Success;
        }

        var skipMessage =
            $"**{skip.Value.Track!.Name}** by **{skip.Value.Track!.Artists}** ({skip.Value.Track!.Duration.HumanizeSecond()})";

        await replier.Reply().To(message).WithEmbed("Now", skipMessage).WithDeletion().SendAsync(ct);

        return Result.Success;
    }
}
