using System.Text;
using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Queue;
using DiscordMusic.Cli.Discord.Music.Streaming;
using DiscordMusic.Core.Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class QueueCommand(IMusicStreamer streamer, IMusicQueue queue, ILogger<QueueCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("queue")]
    [Alias("q")]
    public async Task LeaveAsync([Remainder] int limit = 10)
    {
        logger.LogTrace("Command queue");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        var tracks = queue.GetAll().ToList();

        if (tracks.Count == 0)
        {
            await CommandReplies.OkAsync(
                Context, logger,
                "Queue",
                "The queue is empty",
                "The queue is empty"
            );

            return;
        }

        var builder = new StringBuilder();

        if (streamer.CurrentTrack is not null)
        {
            builder.AppendLine($"Now playing: {streamer.CurrentTrack.Title} - {streamer.CurrentTrack.Author}");
            builder.AppendLine();
        }

        builder.AppendLine($"Queue (next {limit} of {tracks.Count} tracks):");
        foreach (var entry in tracks.Select((track, index) => new { index, track }))
        {
            if (entry.index >= limit)
            {
                break;
            }

            var counter = $"{entry.index + 1}".PadRight(2 + $"{limit}".Length);
            builder.AppendLine($"{counter} {entry.track.Title} - {entry.track.Author}");
        }

        var queueText = builder.ToString();
        logger.LogTrace("Queue text: {QueueText}", queueText);
        await CommandReplies.OkAsync(
            Context, logger,
            "Queue",
            queueText,
            queueText
        );
    }
}
