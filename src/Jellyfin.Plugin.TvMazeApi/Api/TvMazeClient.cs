using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi.Api;

/// <summary>
/// TVmaze API client. All API calls go through a strict sliding-window rate limiter,
/// HTTP 429 pauses all calls and retries, and responses are cached in memory
/// (identical concurrent requests share one call).
/// </summary>
public sealed partial class TvMazeClient : IDisposable
{
    public const string BaseUrl = "https://api.tvmaze.com/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Func<PluginConfiguration> _config;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly SlidingWindowRateLimiter _limiter;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public TvMazeClient(HttpMessageHandler handler, Func<PluginConfiguration> config, TimeProvider time, ILogger<TvMazeClient> logger)
    {
        _config = config;
        _time = time;
        _logger = logger;
        _limiter = new SlidingWindowRateLimiter(time);
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        };
        var version = typeof(TvMazeClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Jellyfin-Plugin-TvMazeApi", version));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Number of HTTP calls actually sent to the API (for diagnostics/tests).</summary>
    public int ApiCallCount => _apiCallCount;

    private int _apiCallCount;

    /// <summary>
    /// One connection, reused (HTTP/2 when available) and closed when idle,
    /// as TVmaze asks clients not to keep several idle connections open.
    /// Redirects are followed manually so every API hit passes the rate limiter.
    /// </summary>
    public static HttpMessageHandler CreateDefaultHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        MaxConnectionsPerServer = 1,
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = DecompressionMethods.All,
    };

    public Task<TvMazeShow?> GetShowAsync(int showId, CancellationToken ct)
        => GetJsonAsync<TvMazeShow>($"shows/{showId}", ct);

    public async Task<IReadOnlyList<TvMazeSearchResult>> SearchShowsAsync(string query, CancellationToken ct)
        => await GetJsonAsync<TvMazeSearchResult[]>("search/shows?q=" + Uri.EscapeDataString(query), ct).ConfigureAwait(false) ?? [];

    public async Task<IReadOnlyList<TvMazeSeason>> GetSeasonsAsync(int showId, CancellationToken ct)
        => await GetJsonAsync<TvMazeSeason[]>($"shows/{showId}/seasons", ct).ConfigureAwait(false) ?? [];

    /// <summary>All episodes incl. specials in one call — far cheaper than one call per episode.</summary>
    public async Task<IReadOnlyList<TvMazeEpisode>> GetEpisodesAsync(int showId, CancellationToken ct)
        => await GetJsonAsync<TvMazeEpisode[]>($"shows/{showId}/episodes?specials=1", ct).ConfigureAwait(false) ?? [];

    public Task<TvMazeEpisode?> GetEpisodeAsync(int episodeId, CancellationToken ct)
        => GetJsonAsync<TvMazeEpisode>($"episodes/{episodeId}", ct);

    public async Task<IReadOnlyList<TvMazeCastMember>> GetCastAsync(int showId, CancellationToken ct)
        => await GetJsonAsync<TvMazeCastMember[]>($"shows/{showId}/cast", ct).ConfigureAwait(false) ?? [];

    public async Task<IReadOnlyList<TvMazeShowImage>> GetShowImagesAsync(int showId, CancellationToken ct)
        => await GetJsonAsync<TvMazeShowImage[]>($"shows/{showId}/images", ct).ConfigureAwait(false) ?? [];

    /// <summary>Looks up a TVmaze show id by external id. <paramref name="source"/> is "thetvdb", "imdb" or "tvrage".</summary>
    public async Task<int?> LookupShowIdAsync(string source, string externalId, CancellationToken ct)
    {
        var response = await GetCachedAsync(
            $"lookup/shows?{source}={Uri.EscapeDataString(externalId)}", ct).ConfigureAwait(false);
        return response is null ? null : ParseShowId(response);
    }

    /// <summary>Downloads an image from the TVmaze CDN (not the API, so not rate limited).</summary>
    public Task<HttpResponseMessage> GetImageResponseAsync(string url, CancellationToken ct)
        => _http.GetAsync(new Uri(url, UriKind.Absolute), HttpCompletionOption.ResponseHeadersRead, ct);

    public void Dispose()
    {
        _http.Dispose();
    }

    internal static int? ParseShowId(string text)
    {
        var match = ShowIdRegex().Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        var body = await GetCachedAsync(path, ct).ConfigureAwait(false);
        return string.IsNullOrEmpty(body) ? null : JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    /// <summary>Returns the body (or redirect target for lookups), or null on 404.</summary>
    private Task<string?> GetCachedAsync(string path, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var ttl = TimeSpan.FromMinutes(Math.Max(0, _config().CacheMinutes));

        while (true)
        {
            if (_cache.TryGetValue(path, out var existing))
            {
                var existingTask = existing.Task;
                bool usable = existingTask.IsCompletedSuccessfully ? existing.Expires > now : !existingTask.IsCompleted;
                if (usable)
                {
                    return existingTask.WaitAsync(ct);
                }

                _cache.TryRemove(new KeyValuePair<string, CacheEntry>(path, existing));
                continue;
            }

            // The shared fetch is not tied to one caller's token, so one cancelled caller can not fail the others.
            var entry = new CacheEntry(() => FetchAsync(path, CancellationToken.None), now + ttl);
            if (!_cache.TryAdd(path, entry))
            {
                continue;
            }

            var task = entry.Task;
            _ = task.ContinueWith(
                _ =>
                {
                    // Errors are never cached; with caching disabled nothing is kept.
                    if (!task.IsCompletedSuccessfully || ttl == TimeSpan.Zero)
                    {
                        _cache.TryRemove(new KeyValuePair<string, CacheEntry>(path, entry));
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            PruneExpired(now);
            return task.WaitAsync(ct);
        }
    }

    private void PruneExpired(DateTimeOffset now)
    {
        if (_cache.Count < 2000)
        {
            return;
        }

        foreach (var kv in _cache)
        {
            if (kv.Value.Task.IsCompleted && kv.Value.Expires <= now)
            {
                _cache.TryRemove(kv);
            }
        }
    }

    private async Task<string?> FetchAsync(string path, CancellationToken ct)
    {
        var config = _config();
        int maxRetries = Math.Max(0, config.MaxRetries);
        var window = TimeSpan.FromSeconds(Math.Max(1, config.WindowSeconds));

        for (int attempt = 0; ; attempt++)
        {
            await _limiter.WaitAsync(config.MaxRequestsPerWindow, window, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _apiCallCount);

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(path, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                var delay = Backoff(attempt, null);
                _logger.LogWarning(ex, "TVmaze request {Path} failed, retrying in {Delay}", path, delay);
                await Task.Delay(delay, _time, ct).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                var status = response.StatusCode;
                if (status == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                    or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    return response.Headers.Location?.ToString() ?? string.Empty;
                }

                if (status == HttpStatusCode.TooManyRequests || (int)status >= 500)
                {
                    if (attempt >= maxRetries)
                    {
                        throw new HttpRequestException(
                            $"TVmaze returned {(int)status} for {path} after {attempt + 1} attempts", null, status);
                    }

                    var delay = Backoff(attempt, response.Headers.RetryAfter);
                    if (status == HttpStatusCode.TooManyRequests)
                    {
                        // Back off globally: every queued request waits, not just this one.
                        _limiter.Pause(delay);
                        _logger.LogInformation("TVmaze rate limit hit (429) on {Path}; pausing {Delay}", path, delay);
                    }
                    else
                    {
                        await Task.Delay(delay, _time, ct).ConfigureAwait(false);
                    }

                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan Backoff(int attempt, RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        var now = _time.GetUtcNow();
        if (retryAfter?.Date is { } date && date > now)
        {
            return date - now;
        }

        // 2, 4, 8, 16, 32, then max 60 seconds.
        return TimeSpan.FromSeconds(Math.Min(60, 2 << Math.Min(attempt, 5)));
    }

    [GeneratedRegex(@"/shows/(\d+)")]
    private static partial Regex ShowIdRegex();

    private sealed class CacheEntry(Func<Task<string?>> factory, DateTimeOffset expires)
    {
        private readonly Lazy<Task<string?>> _lazy = new(factory, LazyThreadSafetyMode.ExecutionAndPublication);

        public Task<string?> Task => _lazy.Value;

        public DateTimeOffset Expires { get; } = expires;
    }
}
