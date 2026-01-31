using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandDispatcher(
    GuildSessionManager guildSessionManager,
    ILogger<VoiceCommandDispatcher> logger
)
{
    public async ValueTask DispatchAsync(VoiceCommand command, ulong guildId, CancellationToken ct)
    {
        if (command.Intent == VoiceCommandIntent.None)
            return;

        var sessionResult = await guildSessionManager.GetSessionAsync(guildId, ct);
        if (sessionResult.IsError)
        {
            logger.LogInformation("Voice command ignored: {Error}", sessionResult);
            return;
        }

        var session = sessionResult.Value;

        switch (command.Intent)
        {
            case VoiceCommandIntent.Play:
                if (!string.IsNullOrWhiteSpace(command.Argument))
                    _ = await session.PlayAsync(command.Argument!, ct);
                break;
            case VoiceCommandIntent.PlayNext:
                if (!string.IsNullOrWhiteSpace(command.Argument))
                    _ = await session.PlayNextAsync(command.Argument!, ct);
                break;
            case VoiceCommandIntent.Pause:
                _ = await session.PauseAsync(ct);
                break;
            case VoiceCommandIntent.Resume:
                _ = await session.ResumeAsync(ct);
                break;
            case VoiceCommandIntent.Skip:
                _ = await session.SkipAsync(toIndex: 1, ct);
                break;
            case VoiceCommandIntent.Queue:
                _ = await session.QueueAsync(ct);
                break;
            case VoiceCommandIntent.NowPlaying:
                _ = await session.NowPlayingAsync(ct);
                break;
            case VoiceCommandIntent.Shuffle:
                _ = await session.ShuffleAsync(ct);
                break;
            case VoiceCommandIntent.QueueClear:
                _ = await session.QueueClearAsync(ct);
                break;
            case VoiceCommandIntent.Lyrics:
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    _ = await session.NowPlayingAsync(ct);
                }
                else
                {
                    logger.LogInformation("Voice lyrics requested: {Query}", command.Argument);
                }
                break;
            case VoiceCommandIntent.Ping:
                logger.LogInformation("Voice ping received");
                break;
            default:
                logger.LogDebug("Unhandled voice command intent: {Intent}", command.Intent);
                break;
        }
    }
}
