using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Queue;
using DiscordMusic.Cli.Discord.Music.Streaming;
using DiscordMusic.Core.Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class SkipCommand(IMusicStreamer streamer, IMusicQueue queue, ILogger<SkipCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("skip")]
    [Alias("s")]
    public async Task SkipAsync([Remainder] int? index = 0)
    {
        logger.LogTrace("Command skip");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        if (streamer.CurrentTrack is null)
        {
            await CommandReplies.OkAsync(
                Context, logger,
                "Now Playing",
                "No track is playing",
                "No track is playing"
            );

            return;
        }

        if (index is null or < 1)
        {
            await streamer.SkipAsync();
            return;
        }

        queue.SkipTo(index.Value - 1);
        await streamer.SkipAsync();
    }
}
