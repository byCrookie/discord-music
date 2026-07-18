using System.CommandLine;

namespace DiscordMusic.Client.Lyrics;

public sealed class LyricsCommand : Command
{
    public LyricsCommand(string[] args)
        : base("lyrics", "Lyric commands")
    {
        Add(new LyricsSearchCommand(args));

        Hidden = true;
    }
}
