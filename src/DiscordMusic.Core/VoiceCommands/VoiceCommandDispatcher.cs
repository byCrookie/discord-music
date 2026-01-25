using DiscordMusic.Core.Discord.Sessions;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.VoiceCommands;

internal sealed class VoiceCommandDispatcher(
    GuildSessionManager guildSessionManager,
    ILogger<VoiceCommandDispatcher> logger)
{
    public async ValueTask DispatchAsync(
        VoiceCommand command,
        VoiceHostContext voiceHostContext,
        CancellationToken ct
    )
    {
        if
            (command.Intent == VoiceCommandIntent.None)
        {
            return;
        }

        switch (command.Intent)
        {
            case VoiceCommandIntent.Play:
                if (!string.IsNullOrWhiteSpace(command.Argument))
                    _ = await voiceHost.PlayAsync(voiceHostContext, command.Argument!, ct);
                break;
            case VoiceCommandIntent.PlayNext:
                if (!string.IsNullOrWhiteSpace(command.Argument))
                    _ = await voiceHost.PlayNextAsync(voiceHostContext, command.Argument!, ct);
                break;
            case VoiceCommandIntent.Pause:
                _ = await voiceHost.PauseAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.Resume:
                _ = await voiceHost.ResumeAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.Skip:
                _ = await voiceHost.SkipAsync(voiceHostContext, toIndex: 1, ct);
                break;
            case VoiceCommandIntent.Queue:
                _ = await voiceHost.QueueAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.NowPlaying:
                _ = await voiceHost.NowPlayingAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.Shuffle:
                _ = await voiceHost.ShuffleAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.QueueClear:
                _ = await voiceHost.QueueClearAsync(voiceHostContext, ct);
                break;
            case VoiceCommandIntent.Lyrics:
                // We don't currently have a "voice reply" mechanism. Best-effort:
                // - If no explicit query is provided, ensure we're connected and can resolve now playing.
                // - If a query is provided, just log it for now.
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    _ = await voiceHost.NowPlayingAsync(voiceHostContext, ct);
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
