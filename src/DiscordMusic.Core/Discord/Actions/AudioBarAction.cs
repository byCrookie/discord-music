using ErrorOr;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord.Actions;

public class AudioBarAction(Replier replier) : IDiscordAction
{
    public string Long => "audiobar";

    public string Short => "ab";

    public string Help =>
        """
        Show the audio bar to control the audio
        Usage: `audiobar`
        """;

    public async Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct)
    {
        await replier
            .Reply()
            .To(message.ChannelId)
            .WithAudioBar()
            .SendAsync(ct);

        return Result.Success;
    }
}
