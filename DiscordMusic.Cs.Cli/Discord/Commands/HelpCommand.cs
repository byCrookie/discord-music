using Discord;
using Discord.Commands;
using DiscordMusic.Cs.Cli.Discord.Options;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace DiscordMusic.Cs.Cli.Discord.Commands;

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
            .AddField($"{prefix}help ({prefix}h)", "Shows this message.")
            .AddField($"{prefix}listen ({prefix}l)", "Toggle listening to cs events.")
            .AddField($"{prefix}playOnFreeze ({prefix}l)pof", "Toggle playing music on freeze time.")
            .WithFooter("Discord Music Bot Cs")
            .WithColor(Color.Blue)
            .Build();

        await ReplyAsync(embed: embed);
    }
}
