using System.IO.Abstractions;

namespace DiscordMusic.Cli.Discord.Music.Download;

public record UpdatedTrack(Track Track, IFileInfo File);
