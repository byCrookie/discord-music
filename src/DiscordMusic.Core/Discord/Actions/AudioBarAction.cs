using DiscordMusic.Core.Discord.Interactions;
using ErrorOr;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordMusic.Core.Discord.Actions;

public class AudioBarAction(RestClient restClient) : IDiscordAction
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
        var audioBar = AudioBar.Create();

        await restClient.SendMessageAsync(
            message.ChannelId,
            new MessageProperties { Components = new List<ComponentProperties> { audioBar } },
            cancellationToken: ct
        );

        return Result.Success;
    }
}
