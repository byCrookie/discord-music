﻿using System.IO.Abstractions;

namespace DiscordMusic.Core.Discord.Music.Download;

public record UpdatedTrack(Track Track, IFileInfo File);
