using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Queue;

public static class QueueModule
{
    public static IHostApplicationBuilder AddQueue(this IHostApplicationBuilder builder)
    {
        builder.Services.AddTransient(typeof(IQueue<>), typeof(Queue<>));
        return builder;
    }
}
