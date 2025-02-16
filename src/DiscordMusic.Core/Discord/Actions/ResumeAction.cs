using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using ErrorOr;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class ResumeAction(IVoiceHost voiceHost, Replier replier, ILogger<ResumeAction> logger) : IDiscordAction
{
    public string Long => "resume";
    public string Short => "r";

    public string Help =>
        """
        Resume the current track
        Usage: `resume`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        logger.LogTrace("Resume");
        var resume = await voiceHost.ResumeAsync(message, ct);

        if (resume.IsError)
        {
            return resume.Errors;
        }

        var resumedMessage = $"""
                              **{resume.Value.Track?.Name}** by **{resume.Value.Track?.Artists}**
                              {resume.Value.AudioStatus.Position.HummanizeSecond()} / {resume.Value.AudioStatus.Length.HummanizeSecond()}
                              """;
        
        await replier
            .ReplyTo(message)
            .WithTitle("Resumed")
            .WithContent(resumedMessage)
            .WithDeletion()
            .SendAsync(ct);
        
        return Result.Success;
    }
}
