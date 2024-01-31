using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class PauseCommand(IMusicStreamer streamer, ILogger<PauseCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("pause", RunMode = RunMode.Async)]
    [Alias("pa")]
    public async Task PauseAsync()
    {
        logger.LogTrace("Command pause");
        
        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }
        
        await streamer.PauseAsync();
    }
}