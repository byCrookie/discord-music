using Flurl;

namespace DiscordMusic.Core.Spotify;

public readonly record struct SpotifyTrack(string Name, string Artists, Url Url);
