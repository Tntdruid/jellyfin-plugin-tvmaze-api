using System.Threading.Channels;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi.Scrobbling;

/// <summary>
/// Listens for episodes marked as played and sends them to TVmaze in batches.
/// Waits a few seconds after the last event, so marking a whole season becomes one call.
/// </summary>
public sealed class ScrobbleService : IHostedService, IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(5);

    private readonly IUserDataManager _userDataManager;
    private readonly TvMazeScrobbler _scrobbler;
    private readonly ILogger<ScrobbleService> _logger;
    private readonly Channel<Pending> _queue = Channel.CreateUnbounded<Pending>();
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    public ScrobbleService(IUserDataManager userDataManager, TvMazeScrobbler scrobbler, ILogger<ScrobbleService> logger)
    {
        _userDataManager = userDataManager;
        _scrobbler = scrobbler;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        _loop = Task.Run(() => RunAsync(_stop.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        _queue.Writer.TryComplete();
        if (_loop is not null)
        {
            // Let queued episodes go out, but don't hold up shutdown for long.
            await Task.WhenAny(_loop, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)).ConfigureAwait(false);
        }

        await _stop.CancelAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _stop.Dispose();
    }

    internal static bool ShouldScrobble(UserDataSaveReason reason, bool played, PluginConfiguration config, Guid userId)
        => played
           && reason is UserDataSaveReason.PlaybackFinished or UserDataSaveReason.TogglePlayed
           && config.GetUser(userId)?.CanScrobble == true;

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || e.Item is not Episode episode
            || !ShouldScrobble(e.SaveReason, e.UserData.Played, config, e.UserId))
        {
            return;
        }

        _queue.Writer.TryWrite(new Pending(e.UserId, episode, TvMazeScrobbler.ToEpoch(e.UserData.LastPlayedDate ?? DateTime.UtcNow)));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var reader = _queue.Reader;
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                var batch = new List<Pending>();
                while (true)
                {
                    while (reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }

                    using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    wait.CancelAfter(Debounce);
                    try
                    {
                        if (!await reader.WaitToReadAsync(wait.Token).ConfigureAwait(false))
                        {
                            break; // queue completed (shutdown)
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        break; // quiet for the debounce period
                    }
                }

                await SendAsync(batch, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task SendAsync(List<Pending> batch, CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return;
        }

        foreach (var group in batch.GroupBy(p => p.UserId))
        {
            var user = config.GetUser(group.Key);
            if (user?.CanScrobble != true)
            {
                continue;
            }

            try
            {
                var items = new List<ScrobbleItem>();
                foreach (var pending in group.DistinctBy(p => p.Episode.Id))
                {
                    if (await _scrobbler.ResolveEpisodeIdAsync(pending.Episode, ct).ConfigureAwait(false) is { } id)
                    {
                        items.Add(new ScrobbleItem(id, pending.MarkedAt));
                    }
                    else
                    {
                        _logger.LogDebug("No TVmaze episode found for {Name}; not marked", pending.Episode.Name);
                    }
                }

                await _scrobbler.ScrobbleAsync(user, items, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Marking episodes as watched on TVmaze failed for {Username}", user.Username);
            }
        }
    }

    private sealed record Pending(Guid UserId, Episode Episode, long MarkedAt);
}
