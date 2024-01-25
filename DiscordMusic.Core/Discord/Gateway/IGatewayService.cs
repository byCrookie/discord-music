namespace DiscordMusic.Core.Discord.Gateway;

public interface IGatewayService
{
    Task<Gateway> GetGatewayBotAsync(CancellationToken ct);
}