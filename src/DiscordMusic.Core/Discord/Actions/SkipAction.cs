using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class SkipAction(IVoiceHost voiceHost, ILogger<SkipAction> logger, Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "skip",
        "Skip to a specific track in the queue. If no position is provided, the next track will be played."
    )]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Skip(
        [SlashCommandParameter(
            Description = "The position of the track to skip to (1-based). Omit to skip to next."
        )]
            int? position = null
    )
    {
        logger.LogTrace("Skip");

        var skipCount = 0;

        if (position is not null)
        {
            if (position.Value < 1)
            {
                await RespondAsync(
                    InteractionCallback.Message("Invalid position"),
                    cancellationToken: cancellation.CancellationToken
                );
                return;
            }

            skipCount = position.Value - 1;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Skip by {skipCount} track(s)
                    This may take a moment...
                    """,
                }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        var skip = await voiceHost.SkipAsync(Context, skipCount, cancellation.CancellationToken);

        if (skip.IsError)
        {
            await ModifyResponseAsync(
                m => m.Content = skip.ToContent(),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        if (skip.Value.Track is null)
        {
            await ModifyResponseAsync(
                m => m.Content = "Queue is empty. Can not skip.",
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        var skipMessage =
            $"**{skip.Value.Track!.Name}** by **{skip.Value.Track!.Artists}** ({skip.Value.Track!.Duration.HumanizeSecond()})";

        await ModifyResponseAsync(
            m =>
                m.Content = $"""
                ### Now
                {skipMessage}
                """,
            cancellationToken: cancellation.CancellationToken
        );
    }
}
