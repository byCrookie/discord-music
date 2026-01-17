using DiscordMusic.Core.Discord.Voice;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordMusic.Core.Discord.Actions;

public class NowPlayingAction(
    IVoiceHost voiceHost,
    ILogger<NowPlayingAction> logger,
    Cancellation cancellation
) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("nowplaying", "Shows the currently playing track.")]
    [RequireChannelMusicAttribute<ApplicationCommandContext>]
    [RequireRoleDj<ApplicationCommandContext>]
    public async Task NowPlaying()
    {
        logger.LogTrace("Nowplaying");
        var nowPlaying = await voiceHost.NowPlayingAsync(Context, cancellation.CancellationToken);

        if (nowPlaying.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = nowPlaying.ToContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        if (nowPlaying.Value.Track is null)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "No track is currently playing",
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        var track = nowPlaying.Value.Track;

        var nowPlayingMessage = $"""
            ### Now Playing
            **{track.Name}** by **{track.Artists}**
            {nowPlaying.Value.AudioStatus.Position.HumanizeSecond()} / {nowPlaying.Value.AudioStatus.Length.HumanizeSecond()}
            """;

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = nowPlayingMessage,
                    Flags = MessageFlags.Ephemeral,
                }
            )
        );
    }
}
