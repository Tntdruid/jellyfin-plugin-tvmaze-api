using System.Net;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using Jellyfin.Plugin.TvMazeApi.Scrobbling;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class ScrobbleTests
{
    private sealed record Captured(HttpMethod Method, string Path, string? Auth, string? Body);

    private sealed class RecordingHandler(Func<Captured, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var captured = new Captured(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(captured);
            return respond(captured);
        }
    }

    private static TvMazeClient Client(RecordingHandler handler)
        => new(handler, () => new PluginConfiguration(), new FakeTimeProvider(), NullLogger<TvMazeClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string BulkOk(int count)
        => "[" + string.Join(",", Enumerable.Repeat("""{"code":200}""", count)) + "]";

    [Fact]
    public async Task ValidateReturnsPremiumLevelAndUsesBasicAuth()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"premium":2}"""));
        using var client = Client(handler);

        var premium = await client.ValidateUserAsync("alice", "secret", CancellationToken.None);

        Assert.Equal(2, premium);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/auth/validate", request.Path);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret")), request.Auth);
    }

    [Fact]
    public async Task ValidateReturnsNullOnWrongLogin()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = Client(handler);

        Assert.Null(await client.ValidateUserAsync("alice", "wrong", CancellationToken.None));
    }

    [Fact]
    public async Task ScrobbleSendsWatchedEpisodesInBatches()
    {
        var handler = new RecordingHandler(r =>
            Json(HttpStatusCode.OK, BulkOk(JsonDocument.Parse(r.Body!).RootElement.GetArrayLength())));
        using var client = Client(handler);
        var items = Enumerable.Range(1, 120).Select(i => new ScrobbleItem(i, 1700000000)).ToList();

        var result = await client.ScrobbleEpisodesAsync("alice", "secret", items, CancellationToken.None);

        Assert.Equal(new ScrobbleResult(120, 0, false), result);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
        Assert.All(handler.Requests, r => Assert.Equal("/v1/scrobble/episodes", r.Path));

        var first = JsonDocument.Parse(handler.Requests[0].Body!).RootElement;
        Assert.Equal(TvMazeClient.ScrobbleBatchSize, first.GetArrayLength());
        Assert.Equal(1, first[0].GetProperty("episode_id").GetInt32());
        Assert.Equal(1700000000, first[0].GetProperty("marked_at").GetInt64());
        Assert.Equal(0, first[0].GetProperty("type").GetInt32());
    }

    [Fact]
    public async Task ScrobbleCountsPartialFailures()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.MultiStatus, """[{"code":200},{"code":404},{"code":200}]"""));
        using var client = Client(handler);

        var result = await client.ScrobbleEpisodesAsync(
            "alice", "secret", [new(1, 0), new(2, 0), new(3, 0)], CancellationToken.None);

        Assert.Equal(new ScrobbleResult(2, 1, false), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ScrobbleReportsAuthFailure(HttpStatusCode status)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status));
        using var client = Client(handler);

        var result = await client.ScrobbleEpisodesAsync("alice", "secret", [new(1, 0)], CancellationToken.None);

        Assert.True(result.AuthFailed);
        Assert.Equal(0, result.Marked);
    }

    [Fact]
    public void OnlyScrobblesPlayedEpisodesForEnabledUsers()
    {
        var enabled = Guid.NewGuid();
        var disabled = Guid.NewGuid();
        var config = new PluginConfiguration
        {
            Users =
            [
                new TvMazeUserConfig { UserId = enabled, Username = "a", ApiKey = "k", ScrobbleEnabled = true },
                new TvMazeUserConfig { UserId = disabled, Username = "b", ApiKey = "k", ScrobbleEnabled = false },
            ],
        };

        Assert.True(ScrobbleService.ShouldScrobble(UserDataSaveReason.PlaybackFinished, true, config, enabled));
        Assert.True(ScrobbleService.ShouldScrobble(UserDataSaveReason.TogglePlayed, true, config, enabled));
        Assert.False(ScrobbleService.ShouldScrobble(UserDataSaveReason.TogglePlayed, false, config, enabled));
        Assert.False(ScrobbleService.ShouldScrobble(UserDataSaveReason.PlaybackProgress, true, config, enabled));
        Assert.False(ScrobbleService.ShouldScrobble(UserDataSaveReason.PlaybackFinished, true, config, disabled));
        Assert.False(ScrobbleService.ShouldScrobble(UserDataSaveReason.PlaybackFinished, true, config, Guid.NewGuid()));
    }

    [Fact]
    public void ScrobbleNeedsCheckboxAndCredentials()
    {
        Assert.False(new TvMazeUserConfig { Username = "a", ApiKey = "k" }.CanScrobble);
        Assert.False(new TvMazeUserConfig { ScrobbleEnabled = true, Username = "a" }.CanScrobble);
        Assert.True(new TvMazeUserConfig { ScrobbleEnabled = true, Username = "a", ApiKey = "k" }.CanScrobble);
    }

    [Fact]
    public void EpochTreatsDatesAsUtc()
    {
        Assert.Equal(0, TvMazeScrobbler.ToEpoch(null));
        Assert.Equal(86400, TvMazeScrobbler.ToEpoch(new DateTime(1970, 1, 2, 0, 0, 0, DateTimeKind.Unspecified)));
    }
}
