using Jellyfin.Plugin.TvMazeApi.Api;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class RateLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AllowsLimitImmediately_ThenBlocksUntilWindowSlides()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(time);

        for (int i = 0; i < 20; i++)
        {
            await limiter.WaitAsync(20, Window, CancellationToken.None);
        }

        var next = limiter.WaitAsync(20, Window, CancellationToken.None);
        Assert.False(next.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(9.9));
        await Task.Yield();
        Assert.False(next.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(0.1));
        await next.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NeverExceedsLimitInAnyWindow()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(time);
        var stamps = new List<DateTimeOffset>();

        var run = Task.Run(async () =>
        {
            for (int i = 0; i < 65; i++)
            {
                await limiter.WaitAsync(20, Window, CancellationToken.None);
                lock (stamps)
                {
                    stamps.Add(time.GetUtcNow());
                }
            }
        });

        while (!run.IsCompleted)
        {
            await Task.Delay(1);
            time.Advance(TimeSpan.FromMilliseconds(250));
        }

        await run;
        for (int i = 20; i < stamps.Count; i++)
        {
            Assert.True(stamps[i] - stamps[i - 20] >= Window, $"call {i} was within the window of call {i - 20}");
        }
    }

    [Fact]
    public async Task PauseBlocksEvenWhenUnderLimit()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(time);

        limiter.Pause(TimeSpan.FromSeconds(5));
        var call = limiter.WaitAsync(20, Window, CancellationToken.None);
        Assert.False(call.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(5));
        await call.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
