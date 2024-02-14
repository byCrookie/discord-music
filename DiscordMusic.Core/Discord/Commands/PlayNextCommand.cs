using Discord;
using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class PlayNextCommand(IMusicStreamer streamer, ILogger<PlayNextCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("playnext", RunMode = RunMode.Async)]
    [Alias("pn")]
    public async Task PlayNextAsync([Remainder] string? argument = null)
    {
        logger.LogTrace("Command playnext");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        var guildUser = (IGuildUser)Context.User;

        await streamer.ConnectAsync(
            Context.Client,
            guildUser.VoiceChannel
        );

        await streamer.PlayNextAsync(argument);
    }
}
