using DiscordMusic.Core.Discord;

namespace DiscordMusic.Core.YouTube.Searching;

public readonly record struct YouTubeSearchRequest(
    string Query,
    DiscordRequestOrigin Origin,
    TrackQueuePlacement Placement = TrackQueuePlacement.Last
);

public enum TrackQueuePlacement
{
    Last,
    Next,
}
