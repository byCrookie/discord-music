using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Rewrite.Actions;

public class PlayAction(ILogger<PlayAction> logger, MusicSessionManager musicSessionManager, Cancellation cancellation)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Play a track. Direct link or search query. Appended to queue.")]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Play([SlashCommandParameter] string query)
    {
        logger.LogTrace("Play");
        
        var session = await musicSessionManager.GetSessionAsync(Context.Guild!, cancellation.CancellationToken);
        
        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToContent(),
                        Flags = MessageFlags.Ephemeral
                    }
                ),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

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
        
        await session.Value.ExecuteCommandAsync(async () =>
        {
            var playResult = await session.Value.VoiceSession.PlayAsync(query, cancellation.CancellationToken);
        
            if (playResult.IsError)
            {
                await ModifyResponseAsync(m => m.Content = playResult.ToContent(), cancellationToken: cancellation.CancellationToken);
                return;
            }
        
            var messageTitle = playResult.Value.Type == VoiceUpdateType.Now ? "Now" : "Next";
        
            if (playResult.Value.Track is null)
            {
                await ModifyResponseAsync(m => m.Content = "No track found", cancellationToken: cancellation.CancellationToken);
                return;
            }
        
            await ModifyResponseAsync(m =>
                m.Content = $"""
                ### {messageTitle}
                **{playResult.Value.Track!.Name}** by **{playResult.Value.Track!.Artists}** ({playResult.Value.Track!.Duration.HumanizeSecond()})
                """,
                cancellationToken: cancellation.CancellationToken
            );
        }, cancellation.CancellationToken);
    }
}
