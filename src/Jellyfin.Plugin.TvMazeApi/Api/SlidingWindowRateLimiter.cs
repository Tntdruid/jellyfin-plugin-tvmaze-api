namespace Jellyfin.Plugin.TvMazeApi.Api;

/// <summary>
/// Strict sliding-window limiter: never more than <c>limit</c> permits within any <c>window</c>.
/// Also supports a global pause (used after HTTP 429).
/// </summary>
internal sealed class SlidingWindowRateLimiter
{
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _stamps = new();
    private long _pausedUntilTicks;

    public SlidingWindowRateLimiter(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Waits until a request may be sent, then records it.</summary>
    public async Task WaitAsync(int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        limit = Math.Max(1, limit);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                var now = _time.GetUtcNow();
                while (_stamps.Count > 0 && now - _stamps.Peek() >= window)
                {
                    _stamps.Dequeue();
                }

                var pausedUntil = new DateTimeOffset(Interlocked.Read(ref _pausedUntilTicks), TimeSpan.Zero);
                TimeSpan wait = TimeSpan.Zero;
                if (pausedUntil > now)
                {
                    wait = pausedUntil - now;
                }
                else if (_stamps.Count >= limit)
                {
                    wait = _stamps.Peek() + window - now;
                }

                if (wait <= TimeSpan.Zero)
                {
                    _stamps.Enqueue(now);
                    return;
                }

                await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Blocks all requests until the given delay has passed.</summary>
    public void Pause(TimeSpan delay)
    {
        long until = (_time.GetUtcNow() + delay).UtcTicks;
        long current;
        do
        {
            current = Interlocked.Read(ref _pausedUntilTicks);
            if (until <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _pausedUntilTicks, until, current) != current);
    }
}
