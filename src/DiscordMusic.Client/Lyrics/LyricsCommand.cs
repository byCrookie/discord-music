using System.CommandLine;

namespace DiscordMusic.Client.Lyrics;

public static class LyricsCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("lyrics", "Lyric commands") { Hidden = true };
        command.Add(LyricsSearchCommand.Create(args));
        return command;
    }
}
