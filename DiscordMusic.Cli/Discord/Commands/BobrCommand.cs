using Discord;
using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Queue;
using DiscordMusic.Cli.Discord.Music.Store;
using DiscordMusic.Cli.Discord.Music.Streaming;
using DiscordMusic.Core.Discord.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class BobrCommand(IMusicStreamer streamer, IMusicQueue queue, IMusicStore store, ILogger<BobrCommand> logger)
    : ModuleBase<CommandContext>
{
    private const string Link = "https://www.youtube.com/watch?v=OHHpYXQyQO4";

    [UsedImplicitly]
    [Command("bobr", RunMode = RunMode.Async)]
    [Alias("b")]
    public async Task PlayAsync()
    {
        logger.LogTrace("Command bobr");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        var guildUser = (IGuildUser)Context.User;

        await streamer.ConnectAsync(
            Context.Client,
            guildUser.VoiceChannel
        );

        var track = store.FindTrack(Link);
        if (track is null)
        {
            await streamer.PlayAsync(Link);
            return;
        }

        logger.LogTrace("Found track {Track}, enqueue next and skip.", track);
        queue.EnqueueNext(track);
        await streamer.SkipAsync();
    }
}
