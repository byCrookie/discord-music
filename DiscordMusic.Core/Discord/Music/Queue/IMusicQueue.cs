namespace DiscordMusic.Core.Discord.Music.Queue;

internal interface IMusicQueue
{
    void Enqueue(Track track);
    void EnqueueNext(Track track);
    bool TryDequeue(out Track? track);
    bool TryPeek(out Track? track);
    IEnumerable<Track> GetAll();
    void Clear();
    void SkipTo(int index);
    int Count();
}
