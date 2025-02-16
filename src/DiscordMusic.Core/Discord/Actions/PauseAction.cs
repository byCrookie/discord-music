using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class PauseAction(IVoiceHost voiceHost, Replier replier, ILogger<PauseAction> logger) : IDiscordAction
{
    public string Long => "pause";
    public string Short => "pa";

    public string Help =>
        """
        Pause the current track
        Usage: `pause`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Pause");
        var pause = await voiceHost.PauseAsync(message, ct);

        if (pause.IsError)
        {
            return pause.Errors;
        }

        var pausedMessage = $"""
                             **{pause.Value.Track?.Name}** by **{pause.Value.Track?.Artists}**
                             {pause.Value.AudioStatus.Position.HummanizeSecond()} / {pause.Value.AudioStatus.Length.HummanizeSecond()}
                             """;
        
        await replier
            .ReplyTo(message)
            .WithTitle("Paused")
            .WithContent(pausedMessage)
            .WithDeletion()
            .SendAsync(ct);
        
        return Result.Success;
    }
}
