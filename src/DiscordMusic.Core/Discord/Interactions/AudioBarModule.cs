using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Discord.Sessions;
using DiscordMusic.Core.Utils;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordMusic.Core.Discord.Interactions;

internal class AudioBarModule(
    GuildSessionManager guildSessionManager,
    Cancellation cancellation,
    ILogger<AudioBarModule> logger
) : ComponentInteractionModule<ButtonInteractionContext>
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
        if (Context.Guild is null)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "This interaction can only be used in a server.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );
            return;
        }

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );
            return;
        }

        var nowPlaying = await session.Value.NowPlayingAsync(cancellation.CancellationToken);

        if (nowPlaying.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = nowPlaying.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );

            return;
        }

        if (nowPlaying.Value.AudioStatus.State == AudioState.Paused)
        {
            var resume = await session.Value.ResumeAsync(cancellation.CancellationToken);

            if (resume.IsError)
            {
                await SafeRespondAsync(
                    InteractionCallback.Message(resume.ToErrorContent()),
                    cancellation.CancellationToken
                );
                return;
            }

            await SafeRespondAsync(
                InteractionCallback.Message(resume.Value.ToValueContent()),
                cancellation.CancellationToken
            );
        }
        else
        {
            var pause = await session.Value.PauseAsync(cancellation.CancellationToken);

            if (pause.IsError)
            {
                await SafeRespondAsync(
                    InteractionCallback.Message(pause.ToErrorContent()),
                    cancellation.CancellationToken
                );
                return;
            }

            await SafeRespondAsync(
                InteractionCallback.Message(pause.Value.ToValueContent()),
                cancellation.CancellationToken
            );
        }
    }

    private async Task ForwardAsync(TimeSpan durationTs)
    {
        if (Context.Guild is null)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "This interaction can only be used in a server.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );
            return;
        }

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );
            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Seeking forward by {durationTs.HumanizeSecond()}...
                    -# This may take a moment...
                    """,
                }
            ),
            cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Forward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
            Seeked forward by {durationTs.HumanizeSecond()}
            {seek.Value.ToValueContent()}
            """;

        await SafeModifyResponseAsync(
            m => m.Content = seekedMessage,
            cancellation.CancellationToken
        );
    }

    private async Task BackwardAsync(TimeSpan durationTs)
    {
        if (Context.Guild is null)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = "This interaction can only be used in a server.",
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );
            return;
        }

        var session = await guildSessionManager.GetSessionAsync(
            Context.Guild.Id,
            cancellation.CancellationToken
        );

        if (session.IsError)
        {
            await SafeRespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Content = session.ToErrorContent(),
                        Flags = MessageFlags.Ephemeral,
                    }
                ),
                cancellation.CancellationToken
            );

            return;
        }

        await SafeRespondAsync(
            InteractionCallback.Message(
                new InteractionMessageProperties
                {
                    Content = $"""
                    ### Seeking backward by {durationTs.HumanizeSecond()}...
                    -# This may take a moment...
                    """,
                }
            ),
            cancellation.CancellationToken
        );

        var seek = await session.Value.SeekAsync(
            durationTs,
            AudioStream.SeekMode.Backward,
            cancellation.CancellationToken
        );

        if (seek.IsError)
        {
            await SafeModifyResponseAsync(
                m => m.Content = seek.ToErrorContent(),
                cancellation.CancellationToken
            );
            return;
        }

        var seekedMessage = $"""
            Seeked backward by {durationTs.HumanizeSecond()}
            {seek.Value.ToValueContent()}
            """;

        await SafeModifyResponseAsync(
            m => m.Content = seekedMessage,
            cancellation.CancellationToken
        );
    }

    private async Task SafeRespondAsync(
        InteractionCallbackProperties callback,
        CancellationToken ct
    )
    {
        try
        {
            await RespondAsync(callback, cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AudioBarModule respond failed (GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId})",
                Context.Guild?.Id,
                Context.Channel?.Id,
                Context.User?.Id
            );
        }
    }

    private async Task SafeModifyResponseAsync(Action<MessageOptions> modify, CancellationToken ct)
    {
        try
        {
            await ModifyResponseAsync(modify, cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AudioBarModule modify failed (GuildId={GuildId}, ChannelId={ChannelId}, UserId={UserId})",
                Context.Guild?.Id,
                Context.Channel?.Id,
                Context.User?.Id
            );
        }
    }
}
