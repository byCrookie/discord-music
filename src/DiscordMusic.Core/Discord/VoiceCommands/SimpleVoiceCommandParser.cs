using System.Text.RegularExpressions;

namespace DiscordMusic.Core.Discord.VoiceCommands;

/// <summary>
/// Very small English parser intended for Whisper transcripts.
/// Requires a wake word by default ("music").
/// Examples:
/// - "music play never gonna give you up"
/// - "music pause"
/// - "music skip"
/// </summary>
public sealed class SimpleVoiceCommandParser : IVoiceCommandParser
{
    private static readonly Regex WakeRegex = new(
        @"\b(music|dj)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private sealed record Rule(Regex Regex, Func<Match, VoiceCommand> Build);
    
    private static readonly Rule[] Rules =
    [
        // Play next <query>
        new(
            new Regex(@"^(?:please\s+)?play\s+next\s+(?<query>.+)$", RegexOptions.Compiled),
            m => new VoiceCommand(VoiceCommandIntent.PlayNext, m.Groups["query"].Value.Trim())
        ),

        // Play <query>
        new(
            new Regex(@"^(?:please\s+)?play\s+(?<query>.+)$", RegexOptions.Compiled),
            m => new VoiceCommand(VoiceCommandIntent.Play, m.Groups["query"].Value.Trim())
        ),

        // Pause/Stop
        new(
            new Regex("^(?:pause|stop)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Pause)
        ),

        // "shut up" / quiet / silence (allow some filler words).
        new(
            new Regex(@"^(?:shut\s+up|quiet|silence)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Pause)
        ),

        // Resume
        new(
            new Regex("^(?:resume|continue)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Resume)
        ),

        // Skip
        new(
            new Regex("^(?:skip|next)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Skip)
        ),

        // Queue
        new(
            new Regex("^(?:queue|list)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Queue)
        ),

        // Shuffle
        new(
            new Regex("^shuffle$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Shuffle)
        ),

        // Now playing
        new(
            new Regex(@"^(?:now\s+playing|nowplaying|what\s+is\s+playing)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.NowPlaying)
        ),

        // Clear queue
        new(
            new Regex(@"^(?:clear\s+queue|queue\s+clear|clear)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.QueueClear)
        ),

        // Lyrics: "lyrics" (use current track) OR "lyrics <query>"
        new(
            new Regex(@"^lyrics(?:\s+(?:for\s+)?)?(?<query>.+)?$", RegexOptions.Compiled),
            m =>
            {
                var q = m.Groups["query"].Success ? m.Groups["query"].Value.Trim() : null;
                return string.IsNullOrWhiteSpace(q)
                    ? new VoiceCommand(VoiceCommandIntent.Lyrics)
                    : new VoiceCommand(VoiceCommandIntent.Lyrics, q);
            }
        ),

        // Ping
        new(
            new Regex(@"^(?:ping|are\s+you\s+there)$", RegexOptions.Compiled),
            _ => new VoiceCommand(VoiceCommandIntent.Ping)
        ),
    ];

    public VoiceCommand Parse(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return VoiceCommand.None;

        var text = transcript.Trim();

        // Require wake word to reduce false positives in a voice channel.
        if (!WakeRegex.IsMatch(text))
            return VoiceCommand.None;

        // Normalize: drop wake word and punctuation-ish.
        text = WakeRegex.Replace(text, " ");
        text = Regex.Replace(text, @"[^a-zA-Z0-9\s]", " ");
        text = Regex.Replace(text, @"\s+,;", " ").Trim().ToLowerInvariant();

        if (text.Length == 0)
        {
            return VoiceCommand.None;
        }

        foreach (var rule in Rules)
        {
            var m = rule.Regex.Match(text);
            if (!m.Success)
            {
                continue;
            }

            var cmd = rule.Build(m);

            // Guard against rules producing empty required args.
            if (cmd.Intent is VoiceCommandIntent.Play or VoiceCommandIntent.PlayNext
                && string.IsNullOrWhiteSpace(cmd.Argument))
            {
                return VoiceCommand.None;
            }

            return cmd;
        }

        return VoiceCommand.None;
    }
}
