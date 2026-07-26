using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DiscordMusic.Core.Observability;

namespace DiscordMusic.Core.Discord.Voice;

internal sealed class VoiceConnectionRegistry
{
    public readonly ConcurrentDictionary<ulong, VoiceConnection?> Mapping = [];

    public VoiceConnectionRegistry()
    {
        DiscordMusicObservability.Meter.CreateObservableGauge(
            "discord.music.discord.voice.connections.current",
            ObserveVoiceConnections,
            unit: "1",
            description: "Current voice connection registrations by guild and state."
        );
        DiscordMusicObservability.Meter.CreateObservableGauge(
            "discord.music.playback.sessions.current",
            ObservePlaybackSessions,
            unit: "1",
            description: "Current playback sessions by guild and playback state."
        );
        DiscordMusicObservability.Meter.CreateObservableGauge(
            "discord.music.playback.position",
            ObservePlaybackPositions,
            unit: "s",
            description: "Current playback position by guild."
        );
    }

    private IEnumerable<Measurement<int>> ObserveVoiceConnections()
    {
        foreach (var (guildId, connection) in Mapping)
        {
            yield return new Measurement<int>(
                1,
                new KeyValuePair<string, object?>("discord.guild.id", guildId.ToString()),
                new KeyValuePair<string, object?>(
                    "state",
                    connection is null ? "starting" : "connected"
                )
            );
        }
    }

    private IEnumerable<Measurement<int>> ObservePlaybackSessions()
    {
        foreach (var (guildId, connection) in Mapping)
        {
            if (connection is null)
            {
                continue;
            }

            yield return new Measurement<int>(
                1,
                new KeyValuePair<string, object?>("discord.guild.id", guildId.ToString()),
                new KeyValuePair<string, object?>(
                    "music.playback.state",
                    connection.PlaybackSession.Snapshot().State.ToString()
                )
            );
        }
    }

    private IEnumerable<Measurement<double>> ObservePlaybackPositions()
    {
        foreach (var (guildId, connection) in Mapping)
        {
            if (connection is null)
            {
                continue;
            }

            var snapshot = connection.PlaybackSession.Snapshot();
            if (snapshot.CurrentTrack is null)
            {
                continue;
            }

            yield return new Measurement<double>(
                snapshot.Position.TotalSeconds,
                new KeyValuePair<string, object?>("discord.guild.id", guildId.ToString()),
                new KeyValuePair<string, object?>("music.playback.state", snapshot.State.ToString())
            );
        }
    }
}
