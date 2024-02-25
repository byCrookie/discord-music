using Discord;
using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class PlayCommand(IMusicStreamer streamer, ILogger<PlayCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("play", RunMode = RunMode.Async)]
    [Alias("p")]
    public async Task PlayAsync([Remainder] string? argument = null)
    {
        logger.LogTrace("Command play");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        var guildUser = (IGuildUser)Context.User;

        await streamer.ConnectAsync(
            Context.Client,
            guildUser.VoiceChannel
        );

        await streamer.PlayAsync(argument);
    }
}
