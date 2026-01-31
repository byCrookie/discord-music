using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.VoiceCommands;

/// <summary>
/// Holds the active per-guild voice-receive subscriptions used by the voice command system.
/// Sessions should not subscribe/unsubscribe the voice command service directly; instead they
/// update this holder when the active <see cref="VoiceClient"/> changes or when a session ends.
/// </summary>
internal interface IVoiceCommandSubscriptions
{
    bool Has(ulong guildId);

    /// <summary>
    /// Ensures voice command processing is wired to <paramref name="voiceClient"/> for <paramref name="guildId"/>.
    /// If a subscription already exists, it is replaced.
    /// </summary>
    void Set(ulong guildId, VoiceClient voiceClient);

    /// <summary>
    /// Disables voice command processing for a guild and releases associated resources.
    /// </summary>
    void Remove(ulong guildId);
}
