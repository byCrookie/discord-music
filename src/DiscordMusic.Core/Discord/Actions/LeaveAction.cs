using DiscordMusic.Core.Discord.Voice;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class LeaveAction(IVoiceHost voiceHost, ILogger<LeaveAction> logger) : IDiscordAction
{
    public string Long => "leave";
    public string Short => "l";

    public string Help =>
        """
        The bot will not participate anymore. It will leave as soon as possible.
        Usage: `leave`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Leave");
        var stop = await voiceHost.DisconnectAsync(ct);

        if (stop.IsError)
        {
            return stop.Errors;
        }
        
        return Result.Success;
    }
}
