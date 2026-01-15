using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class SeekForwardAction(IVoiceHost voiceHost, Replier replier, ILogger<SeekForwardAction> logger)
    : IDiscordAction
{
    public string Long => "seekforward";
    public string Short => "sf";

    public string Help =>
        """
            Seek forward by a duration in the current track
            Usage: `seekforward <duration>`
            `<duration>` - The duration to seek forward
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

        logger.LogTrace("Seeking foward by {Duration}", duration);
        var seek = await voiceHost.SeekAsync(message, duration, AudioStream.SeekMode.Forward, ct);

        if (seek.IsError)
        {
            return seek.Errors;
        }

        var seekedMessage = $"""
            **{seek.Value.Track?.Name}** by **{seek.Value.Track?.Artists}**
            {seek.Value.AudioStatus.Position.HumanizeSecond()} / {seek.Value.AudioStatus.Length.HumanizeSecond()}
            """;

        await replier
            .Reply()
            .To(message)
            .WithEmbed($"Seeked forward by {duration.HumanizeSecond()}", seekedMessage)
            .WithDeletion()
            .SendAsync(ct);

        return Result.Success;
    }
}
