using DiscordMusic.Core.Discord.Clients;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace DiscordMusic.Core.Discord.Gateway;

internal sealed class GatewayService : IGatewayService
{
    private readonly IDiscordBotClient _discordBotClient;
    private readonly ILogger<GatewayService> _logger;

    public GatewayService(IDiscordBotClient discordBotClient, ILogger<GatewayService> logger)
    {
        _discordBotClient = discordBotClient;
        _logger = logger;
    }

    public async Task<Gateway> GetGatewayBotAsync(CancellationToken ct)
    {
        _logger.LogDebug("Getting gateway bot information");

        var response = await _discordBotClient
            .GetAsync("/gateway/bot", ct: ct)
            .ReceiveJson<GatewayResponse>();

        return new Gateway(
            response.Url,
            response.Shards,
            new SessionStartLimit(
                response.SessionStartLimit.Total,
                response.SessionStartLimit.Remaining,
                response.SessionStartLimit.ResetAfter,
                response.SessionStartLimit.MaxConcurrency
            ));
    }
}