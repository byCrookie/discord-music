using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord.Interactions;

internal class AudioBarModule(GuildSessionManager guildSessionManager, Cancellation cancellation)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    public const string FastBackwardButton = "fastBackward";
    public const string BackwardButton = "backward";
    public const string PlayPauseButton = "playpause";
    public const string ForwardButton = "forward";
    public const string FastForwardButton = "fastForward";

    [ComponentInteraction(FastBackwardButton)]
    public async Task FastBackward()
    {
        var duration = TimeSpan.FromSeconds(30);
        await BackwardAsync(duration);
    }

    [ComponentInteraction(BackwardButton)]
    public async Task Backward()
    {
        var duration = TimeSpan.FromSeconds(10);
        await BackwardAsync(duration);
    }

    [ComponentInteraction(ForwardButton)]
    public async Task Forward()
    {
        var duration = TimeSpan.FromSeconds(10);
        await ForwardAsync(duration);
    }

    [ComponentInteraction(FastForwardButton)]
    public async Task FastForward()
    {
        var duration = TimeSpan.FromSeconds(30);
        await ForwardAsync(duration);
    }

    [ComponentInteraction(PlayPauseButton)]
    public async Task PlayPause()
    {
        var session =
            await guildSessionManager.GetSessionAsync(Context.Guild!.Id,
                cancellation.CancellationToken);

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        var nowPlaying = await session.Value.NowPlayingAsync(cancellation.CancellationToken);

        if (nowPlaying.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = nowPlaying.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );

            return;
        }

        if (nowPlaying.Value.AudioStatus.State == AudioState.Paused)
        {
            var resume = await session.Value.ResumeAsync(cancellation.CancellationToken);

            if (resume.IsError)
            {
                await RespondAsync(
                    InteractionCallback.Message(resume.ToErrorContent()),
                    cancellationToken: cancellation.CancellationToken
                );
                return;
            }

            await RespondAsync(
                InteractionCallback.Message(resume.Value.ToValueContent()),
                cancellationToken: cancellation.CancellationToken
            );
        }
        else
        {
            var pause = await session.Value.PauseAsync(cancellation.CancellationToken);

            if (pause.IsError)
            {
                await RespondAsync(
                    InteractionCallback.Message(pause.ToErrorContent()),
                    cancellationToken: cancellation.CancellationToken
                );
                return;
            }

            await RespondAsync(
                InteractionCallback.Message(pause.Value.ToValueContent()),
                cancellationToken: cancellation.CancellationToken
            );
        }
    }

    private async Task ForwardAsync(TimeSpan durationTs)
    {
        var session =
            await guildSessionManager.GetSessionAsync(Context.Guild!.Id,
                cancellation.CancellationToken);

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                )
            );
            return;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                               ### Seeking forward by {durationTs.HumanizeSecond()}...
                               This may take a moment...
                               """,
                }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Forward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await ModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
                             Seeked forward by {durationTs.HumanizeSecond()}
                             {seek.Value.ToValueContent()}
                             """;

        await ModifyResponseAsync(
            m =>
                m.Content = seekedMessage,
            cancellationToken: cancellation.CancellationToken
        );
    }

    private async Task BackwardAsync(TimeSpan durationTs)
    {
        var session =
            await guildSessionManager.GetSessionAsync(Context.Guild!.Id,
                cancellation.CancellationToken);

        if (session.IsError)
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ), cancellationToken: cancellation.CancellationToken);

            return;
        }

        await RespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                               ### Seeking backward by {durationTs.HumanizeSecond()}...
                               This may take a moment...
                               """,
                }
            ),
            cancellationToken: cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Backward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await ModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                cancellationToken: cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
                             Seeked backward by {durationTs.HumanizeSecond()}
                             {seek.Value.ToValueContent()}
                             """;

        await ModifyResponseAsync(
            m => m.Content = seekedMessage,
            cancellationToken: cancellation.CancellationToken
        );
    }
}
