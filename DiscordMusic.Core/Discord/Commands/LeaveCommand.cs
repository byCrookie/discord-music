using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class LeaveCommand(
    IMusicStreamer streamer,
    ILogger<LeaveCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("leave", RunMode = RunMode.Async)]
    [Alias("l")]
    public async Task LeaveAsync()
    {
        logger.LogTrace("Command leave");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        await streamer.DisconnectAsync();
    }
}
