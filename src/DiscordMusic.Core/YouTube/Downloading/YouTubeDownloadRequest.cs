using DiscordMusic.Core.Discord;
using DiscordMusic.Core.Tracks;

namespace DiscordMusic.Core.YouTube.Downloading;

public readonly record struct YouTubeDownloadRequest(Track Track, DiscordRequestOrigin Origin);
