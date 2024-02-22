using Discord;
using Discord.Commands;
using DiscordMusic.Core.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Core.Discord.Commands;

internal class HelpCommand(IOptions<DiscordOptions> discordOptions, ILogger<HelpCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("help")]
    [Alias("h")]
    public async Task HelpAsync()
    {
        logger.LogTrace("Command help");

        var prefix = discordOptions.Value.Prefix;

        var embed = new EmbedBuilder()
            .WithTitle("Commands")
            .WithDescription("List of available commands:")
            .AddField($"{prefix}join ({prefix}j)", "Joins the voice channel.")
            .AddField($"{prefix}play ({prefix}p)", "Queues a url/search from YouTube or resumes if is paused.")
            .AddField($"{prefix}playnext ({prefix}pn)",
                "Queues a url/search from YouTube to play next or resumes if is paused.")
            .AddField($"{prefix}nowplaying ({prefix}np)", "Shows the current track.")
            .AddField($"{prefix}lyrics ({prefix}ly)", "Shows the lyrics of the current track.")
            .AddField($"{prefix}skip [index] ({prefix}s [index])",
                "Skips the current track or index given. Index is optional. Default is 1.")
            .AddField($"{prefix}pause ({prefix}pa)", "Pauses the current track.")
            .AddField($"{prefix}queue [limit] ({prefix}q [limit])",
                "Shows the current queue. Limit is optional. Default is 10.")
            .AddField($"{prefix}clear ({prefix}c)", "Clears the current queue.")
            .AddField($"{prefix}leave ({prefix}l)", "Leaves the voice channel.")
            .AddField($"{prefix}help ({prefix}h)", "Shows this message.")
            .WithFooter("Discord Music Bot")
            .WithColor(Color.Blue)
            .Build();

        await ReplyAsync(embed: embed);
    }
}
