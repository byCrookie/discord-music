using DiscordMusic.Core.Discord.CommandSupport;
using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Playback;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Commands;

internal class AudioBarAction(
    ILogger<AudioBarAction> logger,
    VoiceConnectionRegistry voiceInstances,
    PlaybackService playbackService
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand(
        "audiobar",
        "Show the current playback progress bar with controls.",
        Contexts = [InteractionContextType.Guild]
    )]
    [RequireChannelMusic<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public InteractionMessageProperties AudioBar()
    {
        logger.LogTrace("AudioBar");

        if (
            !VoiceCommandGuard.TryGetPlaybackSession(
                Context,
                voiceInstances,
                playbackService,
                out var session,
                out _,
                out var error
            )
        )
        {
            return error;
        }

        return new InteractionMessageProperties()
            .WithContent(AudioBarRenderer.Render(session.Snapshot()))
            .AddComponents([AudioBarComponents.Create()]);
    }
}
