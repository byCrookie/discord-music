using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class SeekAction(IVoiceHost voiceHost, Replier replier, ILogger<SeekAction> logger) : IDiscordAction
{
    public string Long => "seek";
    public string Short => "sk";

    public string Help =>
        """
            Seek to a specific time in the current track
            Usage: `seek <position>`
            `<position>` - The position to seek to (e.g. hh:mm:ss). Precision is in seconds.
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            return Error.Validation(description: "No arguments provided. Usage: seek <position>");
        }

        if (!TimeSpanParser.TryParse(args[0], out var position))
        {
            return Error.Validation(description: "Not a valid duration (<position>) Usage: seek <position>");
        }

        logger.LogTrace("Seeking to {Position}", position);
        var seek = await voiceHost.SeekAsync(message, position, AudioStream.SeekMode.Position, ct);

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
            .WithEmbed($"Seeked to {position.HummanizeSecond()}", seekedMessage)
            .WithDeletion()
            .SendAsync(ct);

        return Result.Success;
    }
}
