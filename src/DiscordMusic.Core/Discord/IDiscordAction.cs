using ErrorOr;
using NetCord.Gateway;

namespace DiscordMusic.Core.Discord;

public interface IDiscordAction
{
    public string Long { get; }
    public string Short { get; }
    public string Help { get; }
    public Task<ErrorOr<Success>> ExecuteAsync(Message message, string[] args, CancellationToken ct);
}
