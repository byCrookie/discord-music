using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class SeekBackwardAction(IVoiceHost voiceHost, Replier replier, ILogger<SeekBackwardAction> logger)
    : IDiscordAction
{
    public string Long => "seekbackward";
    public string Short => "sb";

    public string Help =>
        """
            Seek backward by a duration in the current track
            Usage: `seekbackward <duration>`
            `<duration>` - The duration to seek backward
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            return Error.Validation(description: "No arguments provided");
        }

        if (!TimeSpanParser.TryParse(args[0], out var duration))
        {
            return Error.Validation(description: "Not a valid duration (<duration>) Usage: seek <duration>");
        }

        logger.LogTrace("Seeking backward by {Duration}", duration);
        var seek = await voiceHost.SeekAsync(message, duration, AudioStream.SeekMode.Backward, ct);

        if (seek.IsError)
        {
            return seek.Errors;
        }

        var seekedMessage = $"""
            **{seek.Value.Track?.Name}** by **{seek.Value.Track?.Artists}**
            {seek.Value.AudioStatus.Position.HummanizeSecond()} / {seek.Value.AudioStatus.Length.HummanizeSecond()}
            """;

        await replier
            .Reply()
            .To(message)
            .WithEmbed($"Seeked backward by {duration.HummanizeSecond()}", seekedMessage)
            .WithDeletion()
            .SendAsync(ct);

        return Result.Success;
    }
}
