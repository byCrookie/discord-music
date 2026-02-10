namespace DiscordMusic.Core.Discord;

/// <summary>
/// Minimal classification of a user's voice state change.
/// Kept independent from NetCord types so it can be unit tested without Discord objects.
/// </summary>
internal enum VoiceStateChange
{
    Unknown = 0,
    Joined,
    Left,
    Moved,
}
