using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Configuration;
using Jellyfin.Plugin.TvMazeApi.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi.Scrobbling;

/// <summary>Turns watched Jellyfin episodes into TVmaze scrobbles.</summary>
public class TvMazeScrobbler
{
    private readonly TvMazeClient _client;
    private readonly ILogger<TvMazeScrobbler> _logger;

    public TvMazeScrobbler(TvMazeClient client, ILogger<TvMazeScrobbler> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Finds the TVmaze episode id: the stored id, otherwise via the series' episode list
    /// (one cached call per series).
    /// </summary>
    public async Task<int?> ResolveEpisodeIdAsync(Episode episode, CancellationToken ct)
    {
        if (TvMazeHelpers.ParseInt(episode.GetProviderId(TvMazeHelpers.ProviderKey)) is { } id)
        {
            return id;
        }

        try
        {
            var found = await TvMazeEpisodeProvider.FindEpisodeAsync(_client, TvMazeEpisodeProvider.ToEpisodeInfo(episode), ct)
                .ConfigureAwait(false);
            return found?.Id;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not find TVmaze episode for {Name}", episode.Name);
            return null;
        }
    }

    public async Task<ScrobbleResult> ScrobbleAsync(TvMazeUserConfig user, IReadOnlyList<ScrobbleItem> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return new ScrobbleResult(0, 0, false);
        }

        var result = await _client.ScrobbleEpisodesAsync(user.Username, user.ApiKey, items, ct).ConfigureAwait(false);
        if (result.AuthFailed)
        {
            _logger.LogWarning(
                "TVmaze rejected the login for {Username}. Check the API key, and note that writing to TVmaze may require premium.",
                user.Username);
        }
        else
        {
            _logger.LogInformation(
                "Marked {Marked} episode(s) as watched on TVmaze for {Username} ({Failed} failed)",
                result.Marked,
                user.Username,
                result.Failed);
        }

        return result;
    }

    public static long ToEpoch(DateTime? date)
        => date is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToUnixTimeSeconds() : 0;
}
