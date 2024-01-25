namespace DiscordMusic.Core.Discord.Gateway;

public record SessionStartLimit(int Total, int Remaining, int ResetAfter, int MaxConcurrency);