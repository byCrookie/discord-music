using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DiscordMusic.Core.Storage;

public static class StorageModule
{
    public static void AddStorage(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<StorageOptions>()
            .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<ITrackStorage, TrackStorage>();
        builder.Services.AddSingleton<IStoragePathProvider, StoragePathProvider>();
        builder.Services.AddSingleton<IStorageCacheTrimmer, StorageCacheTrimmer>();
        builder.Services.AddHostedService<StorageCacheWatcherService>();
    }
}
