using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class PlayNextAction(
    IVoiceHost voiceHost,
    ILogger<PlayNextAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("playnext", "Play a track. Direct link or search query. Prepended to queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task PlayNext(
        [SlashCommandParameter(
            Description = "Direct link or search query. Youtube and Spotify (search only)."
        )]
            string query
    )
    {
        logger.LogTrace("Playnext");

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Searching for **{query}**
                    This may take a moment...
                    """,
                }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        var play = await voiceHost.PlayNextAsync(Context, query, cancellation.CancellationToken);

        if (play.IsError)
        {
            await ModifyResponseAsync(m => m.Content = play.ToContent());
            return;
        }

        var messageTitle = play.Value.Type == VoiceUpdateType.Now ? "Now" : "Next";

        if (play.Value.Track is null)
        {
            await ModifyResponseAsync(m => m.Content = "No track found");
            return;
        }

        await ModifyResponseAsync(m =>
            m.Content = $"""
            ### {messageTitle}
            **{play.Value.Track!.Name}** by **{play.Value.Track!.Artists}** ({play.Value.Track!.Duration.HumanizeSecond()})"
            """
        );
    }
}
