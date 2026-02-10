using DiscordMusic.Core.Discord.Sessions;
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
                {
                    var play = await session.PlayAsync(command.Argument!, ct);
                    await session.ReportIfErrorAsync(play, ct);
                }
                break;
            case VoiceCommandIntent.PlayNext:
                if (!string.IsNullOrWhiteSpace(command.Argument))
                {
                    var playNext = await session.PlayNextAsync(command.Argument!, ct);
                    await session.ReportIfErrorAsync(playNext, ct);
                }
                break;
            case VoiceCommandIntent.Pause:
            {
                var pause = await session.PauseAsync(ct);
                await session.ReportIfErrorAsync(pause, ct);
                break;
            }
            case VoiceCommandIntent.Resume:
            {
                var resume = await session.ResumeAsync(ct);
                await session.ReportIfErrorAsync(resume, ct);
                break;
            }
            case VoiceCommandIntent.Skip:
            {
                var skip = await session.SkipAsync(toIndex: 1, ct);
                await session.ReportIfErrorAsync(skip, ct);
                break;
            }
            case VoiceCommandIntent.Queue:
            {
                var queue = await session.QueueAsync(ct);
                await session.ReportIfErrorAsync(queue, ct);
                break;
            }
            case VoiceCommandIntent.NowPlaying:
            {
                var nowPlaying = await session.NowPlayingAsync(ct);
                await session.ReportIfErrorAsync(nowPlaying, ct);
                break;
            }
            case VoiceCommandIntent.Shuffle:
            {
                var shuffle = await session.ShuffleAsync(ct);
                await session.ReportIfErrorAsync(shuffle, ct);
                break;
            }
            case VoiceCommandIntent.QueueClear:
            {
                var clear = await session.QueueClearAsync(ct);
                await session.ReportIfErrorAsync(clear, ct);
                break;
            }
            case VoiceCommandIntent.Lyrics:
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    var nowPlaying = await session.NowPlayingAsync(ct);
                    await session.ReportIfErrorAsync(nowPlaying, ct);
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
