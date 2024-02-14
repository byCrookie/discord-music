using Discord;
using Discord.Commands;
using DiscordMusic.Core.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Commands;

internal class BobrCommand(IMusicStreamer streamer, ILogger<BobrCommand> logger) : ModuleBase<CommandContext>
{
    private const string Link = "https://www.youtube.com/shorts/WgY5Q5d9SNQ";

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

        await streamer.PlayAsync(Link);
    }
}
