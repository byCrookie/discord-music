using System.Text;
using DiscordMusic.Core.Audio;
using DiscordMusic.Core.Utils;

namespace DiscordMusic.Core.Discord.Sessions;

public record AudioUpdate(Track? Track, Track? NextTrack, AudioStatus AudioStatus)
{
    public string ToValueContent()
    {
        var content = new StringBuilder();

        if (Track is not null)
        {
            content.AppendLine(
                $"**Now Playing ({AudioStatus.State}):** {Track.Name} by {Track.Artists} ({AudioStatus.Position.HumanizeSecond()} / {AudioStatus.Length.HumanizeSecond()})");
        }
        else
        {
            content.AppendLine("**Now Playing:** None");
        }

        if (NextTrack is not null)
        {
            content.AppendLine(
                $"**Up Next:** {NextTrack.Name} by {NextTrack.Artists} - ({NextTrack.Duration.HumanizeSecond()})");
        }
        else
        {
            content.AppendLine("**Up Next:** None");
        }

        return content.ToString();
    }
};
