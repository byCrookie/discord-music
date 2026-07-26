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
        unit: "1",
        description: "Discord command invocations."
    );
    public static readonly Histogram<double> DiscordCommandDuration = Meter.CreateHistogram<double>(
        "discord.music.discord.command.duration",
        unit: "s",
        description: "Discord command processing duration."
    );
    public static readonly Counter<long> ExternalRequests = Meter.CreateCounter<long>(
        "discord.music.external.requests",
        unit: "1",
        description: "Requests to external services and command-line tools."
    );
    public static readonly Histogram<double> ExternalRequestDuration =
        Meter.CreateHistogram<double>(
            "discord.music.external.request.duration",
            unit: "s",
            description: "External service and command-line tool request duration."
        );
    public static readonly Counter<long> ExternalRateLimits = Meter.CreateCounter<long>(
        "discord.music.external.rate_limits",
        unit: "1",
        description: "Rate limit responses from external services."
    );
    public static readonly Counter<long> TrackCacheLookups = Meter.CreateCounter<long>(
        "discord.music.storage.track_cache.lookups",
        unit: "1",
        description: "Track audio cache lookups."
    );
    public static readonly Counter<long> DiscordPreconditionChecks = Meter.CreateCounter<long>(
        "discord.music.discord.precondition.checks",
        unit: "1",
        description: "Discord command precondition checks."
    );
    public static readonly Counter<long> DiscordFeedbackMessages = Meter.CreateCounter<long>(
        "discord.music.discord.feedback.messages",
        unit: "1",
        description: "Discord feedback message delivery attempts."
    );
    public static readonly Counter<long> VoiceDisconnects = Meter.CreateCounter<long>(
        "discord.music.discord.voice.disconnects",
        unit: "1",
        description: "Discord voice disconnects."
    );
    public static readonly Counter<long> CliCommands = Meter.CreateCounter<long>(
        "discord.music.cli.commands",
        unit: "1",
        description: "CLI command invocations."
    );
    public static readonly Histogram<double> CliCommandDuration = Meter.CreateHistogram<double>(
        "discord.music.cli.command.duration",
        unit: "s",
        description: "CLI command processing duration."
    );
    public static readonly Counter<long> StorageWatcherEvents = Meter.CreateCounter<long>(
        "discord.music.storage.watcher.events",
        unit: "1",
        description: "Storage cache watcher events."
    );
    public static readonly Counter<long> StorageOperations = Meter.CreateCounter<long>(
        "discord.music.storage.operations",
        unit: "1",
        description: "Storage filesystem operations."
    );
    public static readonly Counter<long> BinaryLocateRequests = Meter.CreateCounter<long>(
        "discord.music.binary.locate.requests",
        unit: "1",
        description: "Configured binary location checks."
    );
    public static readonly Counter<long> YouTubeToolLoads = Meter.CreateCounter<long>(
        "discord.music.youtube.tools.loads",
        unit: "1",
        description: "YouTube tool location load attempts."
    );
    public static readonly Counter<long> PlaybackLoops = Meter.CreateCounter<long>(
        "discord.music.playback.loops",
        unit: "1",
        description: "Playback loop lifecycle events."
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

    public static Activity? StartCliCommandActivity(string commandName)
    {
        var activity = StartActivity("cli.command");
        SetTag(activity, "cli.command.name", commandName);
        return activity;
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

    public static Activity? StartDiscordPreconditionActivity(
        string precondition,
        ulong? guildId,
        ulong userId
    )
    {
        var activity = StartActivity("discord.precondition");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("discord.precondition.name", precondition);
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

    public static async Task<int> TrackCliCommandAsync(
        string commandName,
        Func<Activity?, Task<int>> action
    )
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "completed";
        using var activity = StartCliCommandActivity(commandName);

        try
        {
            var exitCode = await action(activity);
            result = exitCode == 0 ? "completed" : "failed";
            activity?.SetStatus(
                exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
                result
            );
            return exitCode;
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
            SetTag(activity, "result", result);
            var tags = new TagList { { "cli.command.name", commandName }, { "result", result } };
            CliCommands.Add(1, tags);
            CliCommandDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
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

    public static void RecordVoiceDisconnect(ulong guildId, string reason)
    {
        var tags = GuildTags(guildId);
        tags.Add("reason", reason);
        VoiceDisconnects.Add(1, tags);
    }

    public static void RecordDiscordPrecondition(
        string precondition,
        ulong? guildId,
        ulong userId,
        string result,
        Activity? activity = null
    )
    {
        SetTag(activity, "result", result);
        activity?.SetStatus(ActivityStatusCode.Ok, result);

        var tags = new TagList
        {
            { "discord.precondition.name", precondition },
            { "discord.user.id", userId.ToString() },
            { "result", result },
        };
        if (guildId is { } id)
        {
            tags.Add("discord.guild.id", id.ToString());
        }

        DiscordPreconditionChecks.Add(1, tags);
    }

    public readonly record struct DiscordCommandResult<T>(T Value, string Result);
}
