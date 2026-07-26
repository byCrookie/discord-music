using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordMusic.Core.Observability;

public static class DiscordMusicObservability
{
    public const string Name = "DiscordMusic.Core";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> DiscordCommands = Meter.CreateCounter<long>(
        "discord.music.discord.commands",
        unit: "{command}",
        description: "Discord command invocations."
    );
    public static readonly Histogram<double> DiscordCommandDuration = Meter.CreateHistogram<double>(
        "discord.music.discord.command.duration",
        unit: "s",
        description: "Discord command processing duration."
    );
    public static readonly Counter<long> VoiceConnections = Meter.CreateCounter<long>(
        "discord.music.discord.voice.connections",
        unit: "{connection}",
        description: "Discord voice connection attempts."
    );
    public static readonly Histogram<double> VoiceConnectionDuration =
        Meter.CreateHistogram<double>(
            "discord.music.discord.voice.connection.duration",
            unit: "s",
            description: "Discord voice connection attempt duration."
        );
    public static readonly Counter<long> SearchRequests = Meter.CreateCounter<long>(
        "discord.music.youtube.search.requests",
        unit: "{request}",
        description: "YouTube search requests."
    );
    public static readonly Histogram<double> SearchDuration = Meter.CreateHistogram<double>(
        "discord.music.youtube.search.duration",
        unit: "s",
        description: "YouTube search processing duration."
    );
    public static readonly Counter<long> TracksQueued = Meter.CreateCounter<long>(
        "discord.music.queue.tracks.queued",
        unit: "{track}",
        description: "Tracks added to the playback queue."
    );
    public static readonly Counter<long> QueueMutations = Meter.CreateCounter<long>(
        "discord.music.queue.mutations",
        unit: "{mutation}",
        description: "Queue mutation operations."
    );
    public static readonly Counter<long> DownloadRequests = Meter.CreateCounter<long>(
        "discord.music.youtube.download.requests",
        unit: "{request}",
        description: "YouTube download requests."
    );
    public static readonly Histogram<double> DownloadDuration = Meter.CreateHistogram<double>(
        "discord.music.youtube.download.duration",
        unit: "s",
        description: "YouTube download processing duration."
    );
    public static readonly Counter<long> PlaybackTracks = Meter.CreateCounter<long>(
        "discord.music.playback.tracks",
        unit: "{track}",
        description: "Playback track attempts."
    );
    public static readonly Histogram<double> PlaybackTrackDuration = Meter.CreateHistogram<double>(
        "discord.music.playback.track.duration",
        unit: "s",
        description: "Playback track duration."
    );
    public static readonly Counter<long> ExternalRequests = Meter.CreateCounter<long>(
        "discord.music.external.requests",
        unit: "{request}",
        description: "Requests to external services and command-line tools."
    );
    public static readonly Histogram<double> ExternalRequestDuration =
        Meter.CreateHistogram<double>(
            "discord.music.external.request.duration",
            unit: "s",
            description: "External service and command-line tool request duration."
        );
    public static readonly Counter<long> StorageTrimRuns = Meter.CreateCounter<long>(
        "discord.music.storage.trim.runs",
        unit: "{run}",
        description: "Storage cache trim runs."
    );
    public static readonly Counter<long> TrackCacheLookups = Meter.CreateCounter<long>(
        "discord.music.storage.track_cache.lookups",
        unit: "{lookup}",
        description: "Track audio cache lookups."
    );
    public static readonly Histogram<long> StorageCacheSize = Meter.CreateHistogram<long>(
        "discord.music.storage.cache.size",
        unit: "By",
        description: "Observed storage cache size."
    );
    public static readonly Counter<long> StorageFilesDeleted = Meter.CreateCounter<long>(
        "discord.music.storage.files.deleted",
        unit: "{file}",
        description: "Storage cache files deleted."
    );
    public static readonly Counter<long> StorageBytesDeleted = Meter.CreateCounter<long>(
        "discord.music.storage.bytes.deleted",
        unit: "By",
        description: "Storage cache bytes deleted."
    );

    public static void SetGuildTag(Activity? activity, ulong guildId)
    {
        SetTag(activity, "discord.guild.id", guildId.ToString());
    }

    public static void SetTag(Activity? activity, string key, object? value)
    {
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(key, value);
        }
    }

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    public static Activity? StartDiscordCommandActivity(
        string commandName,
        ulong? guildId,
        ulong userId
    )
    {
        var activity = StartActivity("discord.command");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("discord.command.name", commandName);
            activity.SetTag("discord.user.id", userId.ToString());
            if (guildId is { } id)
            {
                activity.SetTag("discord.guild.id", id.ToString());
            }
        }

        return activity;
    }

    public static TagList GuildTags(ulong guildId)
    {
        return new TagList { { "discord.guild.id", guildId.ToString() } };
    }

    public static TagList DiscordCommandTags(
        string commandName,
        string result,
        ulong? guildId = null
    )
    {
        var tags = new TagList { { "discord.command.name", commandName }, { "result", result } };
        if (guildId is { } id)
        {
            tags.Add("discord.guild.id", id.ToString());
        }

        return tags;
    }

    public static DiscordCommandResult<T> CommandResult<T>(T value, string result = "completed")
    {
        return new DiscordCommandResult<T>(value, result);
    }

    public static T TrackDiscordCommand<T>(
        string commandName,
        ulong? guildId,
        ulong userId,
        TimeProvider timeProvider,
        Func<Activity?, DiscordCommandResult<T>> action
    )
    {
        var startedAt = timeProvider.GetTimestamp();
        var result = "completed";
        using var activity = StartDiscordCommandActivity(commandName, guildId, userId);

        try
        {
            var commandResult = action(activity);
            result = commandResult.Result;
            activity?.SetStatus(ActivityStatusCode.Ok, result == "completed" ? null : result);
            return commandResult.Value;
        }
        catch (Exception ex)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            RecordDiscordCommand(commandName, guildId, result, startedAt, timeProvider);
        }
    }

    public static async Task<T> TrackDiscordCommandAsync<T>(
        string commandName,
        ulong? guildId,
        ulong userId,
        TimeProvider timeProvider,
        Func<Activity?, Task<DiscordCommandResult<T>>> action
    )
    {
        var startedAt = timeProvider.GetTimestamp();
        var result = "completed";
        using var activity = StartDiscordCommandActivity(commandName, guildId, userId);

        try
        {
            var commandResult = await action(activity);
            result = commandResult.Result;
            activity?.SetStatus(ActivityStatusCode.Ok, result == "completed" ? null : result);
            return commandResult.Value;
        }
        catch (Exception ex)
        {
            result = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            RecordDiscordCommand(commandName, guildId, result, startedAt, timeProvider);
        }
    }

    private static void RecordDiscordCommand(
        string commandName,
        ulong? guildId,
        string result,
        long startedAt,
        TimeProvider timeProvider
    )
    {
        var tags = DiscordCommandTags(commandName, result, guildId);
        DiscordCommands.Add(1, tags);
        DiscordCommandDuration.Record(timeProvider.GetElapsedTime(startedAt).TotalSeconds, tags);
    }

    public static TagList ExternalRequestTags(string system, string operation, string result)
    {
        return new TagList
        {
            { "server.address", system },
            { "operation", operation },
            { "result", result },
        };
    }

    public static void RecordTrackCacheLookup(ulong guildId, string source, bool hit)
    {
        var tags = GuildTags(guildId);
        tags.Add("source", source);
        tags.Add("result", hit ? "hit" : "miss");
        TrackCacheLookups.Add(1, tags);
    }

    public readonly record struct DiscordCommandResult<T>(T Value, string Result);
}
