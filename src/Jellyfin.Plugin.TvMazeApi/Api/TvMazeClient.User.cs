using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TvMazeApi.Api;

/// <summary>TVmaze user API (HTTP Basic: TVmaze username + API key). Never cached; still rate limited.</summary>
public sealed partial class TvMazeClient
{
    /// <summary>Max episodes per scrobble request.</summary>
    public const int ScrobbleBatchSize = 50;

    /// <summary>Checks the credentials. Returns the premium level (0 none, 1 Bronze, 2 Silver, 3 Gold), or null if login failed.</summary>
    public async Task<int?> ValidateUserAsync(string username, string apiKey, CancellationToken ct)
    {
        var result = await SendAsync(
            () => WithAuth(new HttpRequestMessage(HttpMethod.Get, "v1/auth/validate"), username, apiKey),
            "v1/auth/validate",
            ct).ConfigureAwait(false);

        if (result.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        EnsureSuccess(result, "v1/auth/validate");
        using var doc = JsonDocument.Parse(result.Body);
        return doc.RootElement.TryGetProperty("premium", out var premium) && premium.ValueKind == JsonValueKind.Number
            ? premium.GetInt32()
            : 0;
    }

    /// <summary>Marks episodes as watched on the user's TVmaze profile.</summary>
    public async Task<ScrobbleResult> ScrobbleEpisodesAsync(
        string username,
        string apiKey,
        IReadOnlyList<ScrobbleItem> episodes,
        CancellationToken ct)
    {
        int marked = 0;
        int failed = 0;
        foreach (var batch in episodes.Chunk(ScrobbleBatchSize))
        {
            var json = JsonSerializer.Serialize(batch);
            var result = await SendAsync(
                () => WithAuth(
                    new HttpRequestMessage(HttpMethod.Post, "v1/scrobble/episodes")
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    },
                    username,
                    apiKey),
                "v1/scrobble/episodes",
                ct).ConfigureAwait(false);

            if (result.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new ScrobbleResult(marked, failed + batch.Length, AuthFailed: true);
            }

            // 200 = all marked, 207 = some failed, 422 = none marked. Each item has its own code.
            if (result.Status is HttpStatusCode.OK or HttpStatusCode.MultiStatus or HttpStatusCode.UnprocessableEntity)
            {
                int ok = CountSuccess(result.Body);
                marked += ok;
                failed += batch.Length - ok;
                continue;
            }

            EnsureSuccess(result, "v1/scrobble/episodes");
        }

        return new ScrobbleResult(marked, failed, AuthFailed: false);
    }

    internal static int CountSuccess(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return doc.RootElement.EnumerateArray().Count(e =>
            e.TryGetProperty("code", out var code) && code.TryGetInt32(out var c) && c is >= 200 and < 300);
    }

    private static HttpRequestMessage WithAuth(HttpRequestMessage request, string username, string apiKey)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return request;
    }

    private static void EnsureSuccess(SendResult result, string path)
    {
        if ((int)result.Status is < 200 or > 299)
        {
            throw new HttpRequestException($"TVmaze returned {(int)result.Status} for {path}", null, result.Status);
        }
    }
}

/// <summary>One episode to mark as watched.</summary>
public sealed record ScrobbleItem(
    [property: JsonPropertyName("episode_id")] int EpisodeId,
    [property: JsonPropertyName("marked_at")] long MarkedAt)
{
    /// <summary>0 = watched.</summary>
    [JsonPropertyName("type")]
    public int Type { get; init; }
}

/// <summary>Outcome of a scrobble call.</summary>
public sealed record ScrobbleResult(int Marked, int Failed, bool AuthFailed);
