using Discord;
using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class JoinCommand(IMusicStreamer streamer, ILogger<JoinCommand> logger) : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("join", RunMode = RunMode.Async)]
    [Alias("j")]
    public async Task JoinAsync()
    {
        logger.LogTrace("Command join");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        var guildUser = (IGuildUser)Context.User;

        await streamer.DisconnectAsync();
        await streamer.ConnectAsync(
            Context.Client,
            guildUser.VoiceChannel
        );
    }
}
