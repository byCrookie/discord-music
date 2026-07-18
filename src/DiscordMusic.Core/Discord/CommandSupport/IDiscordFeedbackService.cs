using DiscordMusic.Core.Tracks;

namespace DiscordMusic.Core.Discord.CommandSupport;

internal interface IDiscordFeedbackService
{
    Task SendPrivateAsync(
        DiscordRequestOrigin origin,
        string message,
        CancellationToken cancellationToken
    );

    Task SendPublicAsync(
        DiscordRequestOrigin origin,
        string message,
        CancellationToken cancellationToken
    );

    Task SendPlaybackFailureAsync(
        DiscordRequestOrigin origin,
        Track track,
        CancellationToken cancellationToken
    );
}
