using DiscordMusic.Core.Audio;

namespace DiscordMusic.Core.Discord.Sessions;

public record AudioUpdate(Track? Track, Track? NextTrack, AudioStatus AudioStatus);
