using System.Collections.Concurrent;
using NetCord.Gateway.Voice;

namespace DiscordMusic.Core.Discord.VoiceCommands;

internal sealed class VoiceCommandSubscriptions(VoiceCommandManager manager)
{
    private readonly ConcurrentDictionary<ulong, IDisposable> _subscriptionTokens = new();

    public bool Has(ulong guildId)
    {
        return _subscriptionTokens.ContainsKey(guildId);
    }

    public void Set(ulong guildId, VoiceClient voiceClient)
    {
        var token = manager.Subscribe(guildId, voiceClient);

        // Replace existing token atomically.
        var old = _subscriptionTokens.AddOrUpdate(
            guildId,
            token,
            (_, existing) =>
            {
                existing.Dispose();
                return token;
            }
        );

        // If AddOrUpdate inserted (no existing), old == token. Nothing else to do.
        if (!ReferenceEquals(old, token))
        {
            // Disposed existing inside update delegate.
        }
    }

    public void Remove(ulong guildId)
    {
        if (_subscriptionTokens.TryRemove(guildId, out var token))
        {
            token.Dispose();
        }

        // Always drop guild buffers/decoder state.
        manager.UnsubscribeGuild(guildId);
    }
}
