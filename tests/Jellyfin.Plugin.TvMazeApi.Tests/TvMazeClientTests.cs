using System.Net;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class TvMazeClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!.PathAndQuery);
            }

            return Task.FromResult(respond(request));
        }
    }

    private static TvMazeClient CreateClient(StubHandler handler, TimeProvider time, PluginConfiguration? config = null)
    {
        config ??= new PluginConfiguration();
        return new TvMazeClient(handler, () => config, time, NullLogger<TvMazeClient>.Instance);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task CachesResponses()
    {
        var handler = new StubHandler(_ => Json("""{"id":1,"name":"Under the Dome"}"""));
        using var client = CreateClient(handler, new FakeTimeProvider());

        var a = await client.GetShowAsync(1, CancellationToken.None);
        var b = await client.GetShowAsync(1, CancellationToken.None);

        Assert.Equal("Under the Dome", a!.Name);
        Assert.Equal("Under the Dome", b!.Name);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CacheExpiresAfterConfiguredTime()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHandler(_ => Json("""{"id":1}"""));
        using var client = CreateClient(handler, time, new PluginConfiguration { CacheMinutes = 60 });

        await client.GetShowAsync(1, CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(61));
        await client.GetShowAsync(1, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task NotFoundReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = CreateClient(handler, new FakeTimeProvider());

        Assert.Null(await client.GetShowAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task LookupFollowsRedirectThroughRateLimiter()
    {
        var handler = new StubHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            resp.Headers.Location = new Uri("https://api.tvmaze.com/shows/82");
            return resp;
        });
        using var client = CreateClient(handler, new FakeTimeProvider());

        var id = await client.LookupShowIdAsync("thetvdb", "121361", CancellationToken.None);

        Assert.Equal(82, id);
        Assert.Equal("/lookup/shows?thetvdb=121361", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task RetriesAfter429WithRetryAfterPause()
    {
        var time = new FakeTimeProvider();
        int calls = 0;
        var handler = new StubHandler(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                return resp;
            }

            return Json("""{"id":1,"name":"ok"}""");
        });
        using var client = CreateClient(handler, time);

        var task = client.GetShowAsync(1, CancellationToken.None);
        await Task.Delay(50);
        Assert.False(task.IsCompleted);
        Assert.Equal(1, calls);

        time.Advance(TimeSpan.FromSeconds(3));
        var show = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("ok", show!.Name);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GivesUpAfterMaxRetries()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var client = CreateClient(handler, time, new PluginConfiguration { MaxRetries = 2 });

        var task = client.GetShowAsync(1, CancellationToken.None);
        while (!task.IsCompleted)
        {
            await Task.Delay(1);
            time.Advance(TimeSpan.FromSeconds(1));
        }

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => task);
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal(3, handler.Requests.Count);

        // Errors are not cached: a later call hits the API again.
        _ = client.GetShowAsync(1, CancellationToken.None);
        await Task.Delay(50);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task ConcurrentIdenticalRequestsShareOneCall()
    {
        var gate = new TaskCompletionSource();
        var handler = new StubHandler(_ =>
        {
            gate.Task.Wait();
            return Json("""{"id":5}""");
        });
        using var client = CreateClient(handler, new FakeTimeProvider());

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() => client.GetShowAsync(5, CancellationToken.None))).ToArray();
        await Task.Delay(100);
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ThrottlesTo20CallsPer10Seconds()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHandler(_ => Json("""{"id":1}"""));
        using var client = CreateClient(handler, time);

        var tasks = Enumerable.Range(1, 25).Select(i => client.GetShowAsync(i, CancellationToken.None)).ToArray();
        await Task.Delay(100);
        Assert.Equal(20, handler.Requests.Count);

        time.Advance(TimeSpan.FromSeconds(10));
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(25, handler.Requests.Count);
    }

    [Theory]
    [InlineData("https://api.tvmaze.com/shows/82", 82)]
    [InlineData("/shows/1", 1)]
    [InlineData("https://api.tvmaze.com/people/1", null)]
    public void ParsesShowIdFromLocation(string location, int? expected)
        => Assert.Equal(expected, TvMazeClient.ParseShowId(location));
}
