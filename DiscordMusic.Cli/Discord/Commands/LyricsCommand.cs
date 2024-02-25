using Discord;
using Discord.Commands;
using DiscordMusic.Cli.Discord.Music.Lyrics;
using DiscordMusic.Cli.Discord.Music.Streaming;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Cli.Discord.Commands;

internal class LyricsCommand(IMusicStreamer streamer, ILyricsService lyricsService, ILogger<SkipCommand> logger)
    : ModuleBase<CommandContext>
{
    [UsedImplicitly]
    [Command("lyrics")]
    [Alias("ly")]
    public async Task LyricsAsync()
    {
        logger.LogTrace("Command lyrics");

        if (!await CommandGuards.IsConnectedToVoiceChannelAsync(Context, logger))
        {
            return;
        }

        if (streamer.CurrentTrack is null)
        {
            await CommandReplies.OkAsync(
                Context, logger,
                "Lyrics",
                "No track is playing",
                "No track is playing"
            );

            return;
        }

        var author = streamer.CurrentTrack.Author;
        var title = streamer.CurrentTrack.Title;
        var lyrics = await lyricsService.GetLyricsAsync(title, author);

        if (lyrics is null)
        {
            await CommandReplies.OkAsync(
                Context, logger,
                "Lyrics",
                "No lyrics found",
                "No lyrics found"
            );

            return;
        }

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"{title} - {author}")
            .WithDescription(lyrics.Text);

        if (!string.IsNullOrWhiteSpace(lyrics.Source))
        {
            embedBuilder = embedBuilder.WithFooter($"Lyrics provided by {lyrics.Source}");
        }

        var embed = embedBuilder
            .WithColor(Color.Blue)
            .Build();

        await ReplyAsync(embed: embed);
    }
}
