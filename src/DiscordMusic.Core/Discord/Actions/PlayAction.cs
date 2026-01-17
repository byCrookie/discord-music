using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class PlayAction(IVoiceHost voiceHost, ILogger<PlayAction> logger, Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Play a track. Direct link or search query. Appended to queue.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Play([SlashCommandParameter] string query)
    {
        logger.LogTrace("Play");

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

        var play = await voiceHost.PlayAsync(Context, query, cancellation.CancellationToken);

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
            **{play.Value.Track!.Name}** by **{play.Value.Track!.Artists}** ({play.Value.Track!.Duration.HumanizeSecond()})
            """
        );
    }
}
