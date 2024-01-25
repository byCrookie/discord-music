namespace DiscordMusic.Core.Discord.Gateway;

public record Gateway(string Url, int Shards, SessionStartLimit SessionStartLimit);