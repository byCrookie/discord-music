using System.Net;
using DiscordMusic.Core.Discord.Options;
using DiscordMusic.Core.Flurl;
using DiscordMusic.Core.Utils;
using Flurl;
using Flurl.Http;
using Flurl.Http.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace DiscordMusic.Core.Discord.Clients;

internal class DiscordBotClient : IDiscordBotClient
{
    private readonly IOptions<DiscordSecrets> _discordSecrets;
    private readonly IDateTimeOffsetProvider _dateTimeOffsetProvider;
    private readonly ILogger<DiscordBotClient> _logger;

    private static IEnumerable<HttpStatusCode> RetryHttpCodes => new[]
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    private const string RateLimitHeader = "x-ratelimit-limit";
    private const string RateLimitRemainingHeader = "x-ratelimit-remaining";
    private const string RateLimitResetHeader = "x-ratelimit-reset";
    private const string RateLimitResetAfterHeader = "x-ratelimit-reset-after";
    private const string RateLimitBucketHeader = "x-ratelimit-bucket";
    private const string RateLimitGlobalHeader = "x-ratelimit-global";
    private const string RateLimitScopeHeader = "x-ratelimit-scope";
    private const string UserAgent = "bycrookie-music (https://github.com/byCrookie/discord-music, 1.0.0)";
    private const string Accept = "application/json";
    private const string BaseUrl = "https://discord.com/api/v10";

    private readonly Lazy<IFlurlClient> _client = new(() => new FlurlClient(BaseUrl)
        .WithHeader(HeaderNames.Accept, Accept)
        .WithHeader(HeaderNames.UserAgent, UserAgent));

    public DiscordBotClient(
        IOptions<DiscordSecrets> discordSecrets,
        IDateTimeOffsetProvider dateTimeOffsetProvider,
        ILogger<DiscordBotClient> logger)
    {
        _discordSecrets = discordSecrets;
        _dateTimeOffsetProvider = dateTimeOffsetProvider;
        _logger = logger;
    }

    public async Task<List<TItem>> ReceiveJsonPagedAsync<TResponse, TItem>(Url url, int perPage,
        Func<TResponse, List<TItem>> getItems, Action<IFlurlRequest>? configure = null, CancellationToken? ct = null)
    {
        var request = _client.Value.Request(url);
        configure?.Invoke(request);
        _logger.LogDebug("Requesting {Url} paged", request.Url);
        return await request
            .WithBotToken(_discordSecrets.Value.Token)
            .SetQueryParam("per_page", perPage)
            .GetPagedJsonAsync(
                getItems,
                (_, _, items) => items.Count == perPage,
                (rq, index) => rq.SetQueryParam("page", index + 1),
                (rq, cancellationToken) => SendAsync(rq, HttpMethod.Get, null, cancellationToken),
                ct ?? CancellationToken.None
            );
    }

    public async Task<string> DownloadFileAsync(Url url, string path, string? fileName = null,
        Action<IFlurlRequest>? configure = null, CancellationToken? ct = null)
    {
        var request = _client.Value.Request(url)
            .WithBotToken(_discordSecrets.Value.Token);
        configure?.Invoke(request);
        _logger.LogDebug("Downloading {Url}", request.Url);
        var file = await request.DownloadFileAsync(path, fileName, cancellationToken: ct ?? CancellationToken.None);
        _logger.LogInformation("Downloaded {Url} to {Path}", request.Url, file);
        return file;
    }

    public async Task<IFlurlResponse> GetAsync(Url url, Action<IFlurlRequest>? configure = null,
        CancellationToken? ct = null)
    {
        var request = _client.Value.Request(url)
            .WithBotToken(_discordSecrets.Value.Token);
        configure?.Invoke(request);
        _logger.LogDebug("Requesting {Url}", request.Url);
        return await SendAsync(request, HttpMethod.Get, null, ct ?? CancellationToken.None);
    }

    public async Task<IFlurlResponse> PostJsonAsync(Url url, object data, Action<IFlurlRequest>? configure = null,
        CancellationToken? ct = null)
    {
        var request = _client.Value.Request(url)
            .WithBotToken(_discordSecrets.Value.Token);
        configure?.Invoke(request);
        _logger.LogDebug("Posting to {Url}", request.Url);
        var content = new CapturedJsonContent(request.Settings.JsonSerializer.Serialize(data));
        return await SendAsync(request, HttpMethod.Post, content, ct ?? CancellationToken.None);
    }

    private async Task<IFlurlResponse> SendAsync(IFlurlRequest request, HttpMethod verb,
        HttpContent? content = null, CancellationToken? ct = null)
    {
        const int maxRetries = 3;
        var delays = Backoff
            .DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: maxRetries)
            .ToArray();

        var resiliencePipeline = new ResiliencePipelineBuilder<IFlurlResponse>()
            .AddRetry(new RetryStrategyOptions<IFlurlResponse>
            {
                ShouldHandle = new PredicateBuilder<IFlurlResponse>()
                    .Handle<FlurlHttpException>(exception =>
                        exception.StatusCode is not null &&
                        RetryHttpCodes.Contains((HttpStatusCode)exception.StatusCode))
                    .HandleResult(response => RetryHttpCodes.Contains((HttpStatusCode)response.StatusCode)),
                DelayGenerator = arguments =>
                {
                    var delay = delays[arguments.AttemptNumber];
                    _logger.LogDebug(
                        "Retry Attempt {Attempt} - Delaying for {Delay} before retrying request to {Verb} - {Url}",
                        arguments.AttemptNumber, delay, verb, request.Url);
                    return ValueTask.FromResult<TimeSpan?>(delay);
                },
                MaxRetryAttempts = maxRetries
            })
            .AddRetry(new RetryStrategyOptions<IFlurlResponse>
            {
                ShouldHandle = new PredicateBuilder<IFlurlResponse>()
                    .HandleResult(response => response.Headers.GetRequired(RateLimitRemainingHeader) == "0"),
                DelayGenerator = arguments =>
                {
                    var rateLimitReset = arguments.Outcome.Result!.Headers.GetRequired(RateLimitResetHeader);
                    var rateLimitResetDateTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(rateLimitReset));
                    var now = _dateTimeOffsetProvider.UtcNow;
                    var delay = rateLimitResetDateTime - now;
                    _logger.LogDebug("RateLimit - Delaying for {Delay} before retrying request to {Verb} - {Url}",
                        delay, verb, request.Url);
                    return ValueTask.FromResult<TimeSpan?>(delay);
                }
            })
            .AddRetry(new RetryStrategyOptions<IFlurlResponse>
            {
                ShouldHandle = new PredicateBuilder<IFlurlResponse>()
                    .Handle<FlurlHttpException>(exception =>
                        !string.IsNullOrWhiteSpace(exception.Call.Response.Headers.Get(RateLimitResetAfterHeader))),
                DelayGenerator = arguments =>
                {
                    var exception = (FlurlHttpException)arguments.Outcome.Exception!;
                    var resetAfter = exception.Call.Response.Headers.GetRequired(RateLimitResetAfterHeader);
                    var delay = TimeSpan.FromSeconds(int.Parse(resetAfter));
                    _logger.LogDebug("RetryAfter - Delaying for {Delay} before retrying request to {Verb} - {Url}",
                        delay, verb, request.Url);
                    return ValueTask.FromResult<TimeSpan?>(delay);
                }
            })
            .Build();

        return await resiliencePipeline.ExecuteAsync(
            async cancellationToken =>
            {
                _logger.LogTrace("Sending {Verb} request to {Url} with content {Content} and headers {Headers}", verb,
                    request.Url,
                    content is not null ? await content.ReadAsStringAsync(cancellationToken) : string.Empty,
                    request.Headers);
                var response = await request.SendAsync(verb, content, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                _logger.LogTrace(
                    "Received {StatusCode} response from {Url} with content {Content} and headers {Headers}",
                    response.StatusCode, request.Url, await response.GetStringAsync(), response.Headers);
                return response;
            },
            ct ?? CancellationToken.None
        );
    }
}