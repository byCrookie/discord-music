using System.Text;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.Discord.Sessions;

public record AudioUpdate(Track? Track, Track? NextTrack, AudioStatus AudioStatus)
{
    public string ToValueContent()
    {
        var content = new StringBuilder();

        content.AppendLine("### Now playing");

        if (Track is not null)
        {
            var length =
                AudioStatus.Length == TimeSpan.Zero ? "live" : AudioStatus.Length.HumanizeSecond();

            content.AppendLine($"**{Track.Name}** — **{Track.Artists}**");
            content.AppendLine(
                $"-# Status: {AudioStatus.State} • {AudioStatus.Position.HumanizeSecond()} / {length}"
            );
        }
        else
        {
            content.AppendLine("Nothing is playing right now.");
            content.AppendLine($"-# Status: {AudioStatus.State}");
        }

        content.AppendLine();
        content.AppendLine("### Up next");

        if (NextTrack is not null)
        {
            var duration =
                NextTrack.Duration == TimeSpan.Zero
                    ? "unknown"
                    : NextTrack.Duration.HumanizeSecond();

            content.AppendLine($"**{NextTrack.Name}** — **{NextTrack.Artists}**");
            content.AppendLine($"-# Duration: {duration}");
        }
        else
        {
            content.AppendLine("Nothing queued.");
        }

        return content.ToString().TrimEnd();
    }
};
