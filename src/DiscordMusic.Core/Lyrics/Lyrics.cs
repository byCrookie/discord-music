using Flurl;

namespace DiscordMusic.Core.Lyrics;

public readonly record struct Lyrics(string Title, string Artist, string Text, Url Url);
