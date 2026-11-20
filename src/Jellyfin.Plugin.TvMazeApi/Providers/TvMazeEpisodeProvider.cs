using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

public class TvMazeEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly TvMazeClient _client;

    public TvMazeEpisodeProvider(TvMazeClient client)
    {
        _client = client;
    }

    public string Name => Plugin.ProviderName;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
    {
        var episode = await FindEpisodeAsync(_client, searchInfo, cancellationToken).ConfigureAwait(false);
        if (episode is null)
        {
            return [];
        }

        var result = new RemoteSearchResult
        {
            Name = episode.Name,
            SearchProviderName = Plugin.ProviderName,
            IndexNumber = episode.Number,
            ParentIndexNumber = episode.Season,
            PremiereDate = TvMazeHelpers.ParseDate(episode.Airdate),
            Overview = TvMazeHelpers.StripHtml(episode.Summary),
            ImageUrl = episode.Image?.Medium ?? episode.Image?.Original,
        };
        result.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(episode.Id));
        return [result];
    }

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Episode>();
        var episode = await FindEpisodeAsync(_client, info, cancellationToken).ConfigureAwait(false);
        if (episode is null)
        {
            return result;
        }

        var item = new Episode
        {
            Name = episode.Name,
            Overview = TvMazeHelpers.StripHtml(episode.Summary),
            PremiereDate = episode.Airstamp?.UtcDateTime ?? TvMazeHelpers.ParseDate(episode.Airdate),
            CommunityRating = episode.Rating?.Average is { } r ? (float)r : null,
            IndexNumber = info.IndexNumber ?? episode.Number,
            ParentIndexNumber = info.ParentIndexNumber ?? episode.Season,
        };
        item.ProductionYear = item.PremiereDate?.Year;
        if (episode.Runtime is { } runtime)
        {
            item.RunTimeTicks = TimeSpan.FromMinutes(runtime).Ticks;
        }

        item.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(episode.Id));

        result.Item = item;
        result.HasMetadata = true;
        return result;
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => await _client.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Matches against the show's full episode list (one cached API call per show)
    /// instead of one call per episode.
    /// </summary>
    internal static async Task<TvMazeEpisode?> FindEpisodeAsync(TvMazeClient client, EpisodeInfo info, CancellationToken ct)
    {
        if (TvMazeHelpers.ParseInt(info.GetProviderId(TvMazeHelpers.ProviderKey)) is { } episodeId)
        {
            var byId = await client.GetEpisodeAsync(episodeId, ct).ConfigureAwait(false);
            if (byId is not null)
            {
                return byId;
            }
        }

        var showId = await TvMazeHelpers.ResolveShowIdAsync(client, info.SeriesProviderIds, null, null, ct).ConfigureAwait(false);
        if (showId is null)
        {
            return null;
        }

        var episodes = await client.GetEpisodesAsync(showId.Value, ct).ConfigureAwait(false);
        return MatchEpisode(episodes, info.ParentIndexNumber, info.IndexNumber, info.PremiereDate);
    }

    internal static EpisodeInfo ToEpisodeInfo(Episode episode) => new()
    {
        IndexNumber = episode.IndexNumber,
        ParentIndexNumber = episode.ParentIndexNumber,
        PremiereDate = episode.PremiereDate,
        ProviderIds = episode.ProviderIds,
        SeriesProviderIds = episode.Series?.ProviderIds ?? new Dictionary<string, string>(),
    };

    internal static TvMazeEpisode? MatchEpisode(IReadOnlyList<TvMazeEpisode> episodes, int? season, int? number, DateTime? airDate)
    {
        if (number is not null)
        {
            if (season is 0)
            {
                // Jellyfin season 0 = specials. TVmaze specials have no number; use airing order.
                var specials = episodes.Where(e => e.Number is null).ToList();
                return number >= 1 && number <= specials.Count ? specials[number.Value - 1] : null;
            }

            var match = episodes.FirstOrDefault(e => e.Season == (season ?? 1) && e.Number == number);
            if (match is not null)
            {
                return match;
            }
        }

        if (airDate is { } date)
        {
            var day = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return episodes.FirstOrDefault(e => e.Airdate == day);
        }

        return null;
    }
}
