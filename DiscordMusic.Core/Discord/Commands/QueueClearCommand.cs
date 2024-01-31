using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Queue;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class QueueClearCommand(IMusicQueue queue, ILogger<QueueClearCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("clear")]
    [Alias("c")]
    public async Task QueueClearAsync()
    {
        logger.LogTrace("Command queue clear");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        if (queue.Count() == 0)
        {
            await CommandReplies.OkAsync(
                Context, logger,
                "Queue",
                "The queue is already empty",
                "The queue is already empty"
            );

            return;
        }
        
        queue.Clear();

        await CommandReplies.OkAsync(
            Context, logger,
            "Queue",
            "The queue has been cleared.",
            "The queue has been cleared."
        );
    }
}