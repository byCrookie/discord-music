using System.CommandLine;

namespace DiscordMusic.Client.Lyrics;

public static class LyricsCommand
{
    public static Command Create(string[] args)
    {
        var command = new Command("lyrics", "Lyric commands") { IsHidden = true };

        command.AddCommand(LyricsSearchCommand.Create(args));
        return command;
    }
}
