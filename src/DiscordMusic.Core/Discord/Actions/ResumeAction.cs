using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class ResumeAction(
    IVoiceHost voiceHost,
    ILogger<ResumeAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("resume", "Resume the current track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task Resume()
    {
        logger.LogTrace("Resume");
        var resume = await voiceHost.ResumeAsync(Context, cancellation.CancellationToken);

        if (resume.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(resume.ToContent()),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        var resumedMessage = $"""
            ### Resumed
            **{resume.Value.Track?.Name}** by **{resume.Value.Track?.Artists}**
            {resume.Value.AudioStatus.Position.HumanizeSecond()} / {resume.Value.AudioStatus.Length.HumanizeSecond()}
            """;

        await RespondAsync(
            InteractionCallback.Message(resumedMessage),
            cancellationToken: cancellation.CancellationToken
        );
    }
}
