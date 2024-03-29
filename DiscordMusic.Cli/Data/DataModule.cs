﻿using Microsoft.Extensions.DependencyInjection;

namespace DiscordMusic.Cli.Data;

internal static class DataModule
{
    public static void AddData(this IServiceCollection services)
    {
        services.AddSingleton<IDataStore, DataStore>();
    }
}
