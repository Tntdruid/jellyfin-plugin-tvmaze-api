using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi.Scrobbling;

/// <summary>Sends every watched episode of each linked user to TVmaze. Run manually from Scheduled Tasks.</summary>
public class SyncWatchedTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly TvMazeScrobbler _scrobbler;
    private readonly ILogger<SyncWatchedTask> _logger;

    public SyncWatchedTask(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        TvMazeScrobbler scrobbler,
        ILogger<SyncWatchedTask> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _scrobbler = scrobbler;
        _logger = logger;
    }

    public string Name => "Sync watched episodes to TVmaze";

    public string Key => "TvMazeApiSyncWatched";

    public string Description => "Marks every episode you have watched in Jellyfin as watched on your TVmaze profile.";

    public string Category => Plugin.ProviderName;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = Plugin.Instance?.Configuration.Users.Where(u => u.CanScrobble).ToList() ?? [];
        if (users.Count == 0)
        {
            _logger.LogInformation("No Jellyfin users have TVmaze marking enabled");
            progress.Report(100);
            return;
        }

        for (int u = 0; u < users.Count; u++)
        {
            var config = users[u];
            var user = _userManager.GetUserById(config.UserId);
            if (user is null)
            {
                continue;
            }

            var episodes = _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                IncludeItemTypes = [BaseItemKind.Episode],
                IsPlayed = true,
                Recursive = true,
            }).OfType<Episode>().ToList();

            var items = new List<ScrobbleItem>();
            for (int i = 0; i < episodes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var episode = episodes[i];
                if (await _scrobbler.ResolveEpisodeIdAsync(episode, cancellationToken).ConfigureAwait(false) is { } id)
                {
                    var data = _userDataManager.GetUserData(user, episode);
                    items.Add(new ScrobbleItem(id, TvMazeScrobbler.ToEpoch(data?.LastPlayedDate)));
                }

                // Finding ids is most of the work; sending is the last 10 % per user.
                progress.Report(100.0 * (u + (0.9 * (i + 1) / episodes.Count)) / users.Count);
            }

            var result = await _scrobbler.ScrobbleAsync(config, items, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "TVmaze sync for {User}: {Watched} watched, {Found} found on TVmaze, {Marked} marked",
                user.Username,
                episodes.Count,
                items.Count,
                result.Marked);
            progress.Report(100.0 * (u + 1) / users.Count);
        }
    }
}
