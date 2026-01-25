using System.Threading.Channels;
using DiscordMusic.Core.FileCache;
using DiscordMusic.Core.YouTube;
using Flurl;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Rewrite.Workers;

public class SearchWorker(Channel<string> searchChannel, IYoutubeSearch youtubeSearch, IFileCache<Url, AudioMetadata> fileCache) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var query in searchChannel.Reader.ReadAllAsync(stoppingToken))
        {
            
        }
    }
}
