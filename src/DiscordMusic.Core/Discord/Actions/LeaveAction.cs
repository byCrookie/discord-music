using DiscordMusic.Core.Discord.Voice;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class LeaveAction(IVoiceHost voiceHost, Replier replier, ILogger<LeaveAction> logger) : IDiscordAction
{
    public string Long => "leave";
    public string Short => "l";

    public string Help =>
        """
        The bot will not participate anymore. It will leave the voice channel after some time.
        Usage: `leave`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Leave");
        var stop = await voiceHost.StopAsync(ct);

        if (stop.IsError)
        {
            return stop.Errors;
        }
        
        await replier
            .ReplyTo(message)
            .WithTitle("Leave")
            .WithContent("I will leave the voice channel soon. Just ignore me.")
            .WithDeletion()
            .SendAsync(ct);
        
        return Result.Success;
    }
}
