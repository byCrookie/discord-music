using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class PauseAction(
    IVoiceHost voiceHost,
    ILogger<PauseAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("pause", "Pause the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Pause()
    {
        logger.LogTrace("Pause");
        var pause = await voiceHost.PauseAsync(Context, cancellation.CancellationToken);

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties { Content = "### Pausing..." }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        if (pause.IsError)
        {
            await ModifyResponseAsync(m => m.Content = pause.ToContent());
            return;
        }

        var pausedMessage = $"""
            ### Paused
            **{pause.Value.Track?.Name}** by **{pause.Value.Track?.Artists}**
            {pause.Value.AudioStatus.Position.HumanizeSecond()} / {pause.Value.AudioStatus.Length.HumanizeSecond()}
            """;

        await ModifyResponseAsync(m => m.Content = pausedMessage);
    }
}
