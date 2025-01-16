using NetCord.Gateway;

namespace DiscordMusic.Core.Discord;

public interface IReplies
{
    public static TimeSpan DefaultDeletionDelay => TimeSpan.FromSeconds(30);

    Task SendDmAsync(Message message, string title, string? description, CancellationToken ct);
    Task SendAsync(Message message, string title, string? description, CancellationToken ct);

    Task SendWithDeletionAsync(
        Message message,
        string title,
        string? description,
        TimeSpan deletionDelay,
        CancellationToken ct
    );

    Task SendWithDeletionAsync(
        ulong channelId,
        string title,
        string? description,
        TimeSpan deletionDelay,
        CancellationToken ct
    );

    Task SendErrorWithDeletionAsync(Message message, string? content, TimeSpan deletionDelay, CancellationToken ct);
    Task SendErrorWithDeletionAsync(ulong channelId, string? content, TimeSpan deletionDelay, CancellationToken ct);
}
