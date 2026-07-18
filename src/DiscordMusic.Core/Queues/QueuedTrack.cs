using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Tracks;

namespace DiscordMusic.Core.Queues;

public readonly record struct QueuedTrack(
    Track Track,
    QueuedTrackStatus Status,
    DiscordRequestOrigin? Origin = null
);
