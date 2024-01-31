using Discord;

namespace DiscordMusic.Core.Discord.Music.Streaming;

internal interface IMusicStreamer
{
    public Track? CurrentTrack { get; }

    Task ConnectAsync(IDiscordClient client, IVoiceChannel channel);
    Task DisconnectAsync();
    
    Task PlayAsync(string? argument);
    Task PlayNextAsync(string? argument);
    Task SkipAsync();
    Task PauseAsync();


    bool CanExecute();
    Task ExecuteAsync(CancellationToken ct);
}