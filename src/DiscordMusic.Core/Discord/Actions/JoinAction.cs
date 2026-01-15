using DiscordMusic.Core.Discord.Voice;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class JoinAction(IVoiceHost voiceHost, ILogger<JoinAction> logger) : IDiscordAction
{
    public string Long => "join";
    public string Short => "j";

    public string Help =>
        """
            The bot will join the voice channel you are in.
            Usage: `join`
            """;

    public async Task<ErrorOr<Success>> ExecuteAsync(
        Message message,
        string[] args,
        CancellationToken ct
    )
    {
        logger.LogTrace("Join");
        var connection = await voiceHost.ConnectAsync(message, ct);
        return connection.IsError ? connection : Result.Success;
    }
}
