using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class PlayNextAction(IVoiceHost voiceHost, Replier replier, ILogger<PlayNextAction> logger) : IDiscordAction
{
    public string Long => "playnext";

    public string Short => "pn";

    public string Help =>
        """
        Play a track. It is prepended to the queue.
        Usage: `play <query>`
        `<query>` - Can be a URL or a search term
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            return Error.Validation(description: "No arguments provided. Usage: playnext <query>");
        }

        logger.LogTrace("Playnext");
        var play = await voiceHost.PlayNextAsync(message, string.Join(" ", args), ct);

        if (play.IsError)
        {
            return play.Errors;
        }

        var messageTitle = play.Value.Type == VoiceUpdateType.Now ? "Now" : "Next";

        if (play.Value.Track is null)
        {
            await replier
                .Reply()
                .To(message)
                .WithEmbed(messageTitle, "No track found")
                .WithDeletion()
                .SendAsync(ct);

            return Result.Success;
        }

        await replier
            .Reply()
            .To(message)
            .WithEmbed(messageTitle, $"**{play.Value.Track!.Name}** by **{play.Value.Track!.Artists}** ({play.Value.Track!.Duration.HummanizeSecond()})")
            .WithDeletion()
            .SendAsync(ct);

        return Result.Success;
    }
}
