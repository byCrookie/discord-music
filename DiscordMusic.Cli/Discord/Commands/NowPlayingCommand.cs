using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Streaming;
using DiscordMusic.Core.Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class NowPlayingCommand(IMusicStreamer streamer, ILogger<NowPlayingCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("nowplaying")]
    [Alias("np")]
    public async Task LeaveAsync()
    {
        logger.LogTrace("Command nowplaying");

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

        await CommandReplies.OkAsync(
            Context, logger,
            "Now Playing",
            $"Now playing: {streamer.CurrentTrack.Title} - {streamer.CurrentTrack.Author} ({streamer.CurrentTrack.Url})",
            "Now playing: {Title} - {Author} ({Url})",
            streamer.CurrentTrack.Title,
            streamer.CurrentTrack.Author,
            streamer.CurrentTrack.Url
        );
    }
}
