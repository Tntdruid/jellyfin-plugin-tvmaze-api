using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

public class TvMazeSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
{
    private readonly TvMazeClient _client;

    public TvMazeSeasonProvider(TvMazeClient client)
    {
        _client = client;
    }

    public string Name => Plugin.ProviderName;

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Season>();
        var season = await FindSeasonAsync(_client, info.SeriesProviderIds, info.IndexNumber, cancellationToken).ConfigureAwait(false);
        if (season is null)
        {
            return result;
        }

        var item = new Season
        {
            IndexNumber = season.Number,
            Overview = TvMazeHelpers.StripHtml(season.Summary),
            PremiereDate = TvMazeHelpers.ParseDate(season.PremiereDate),
            EndDate = TvMazeHelpers.ParseDate(season.EndDate),
        };
        if (!string.IsNullOrWhiteSpace(season.Name))
        {
            item.Name = season.Name;
        }

        item.ProductionYear = item.PremiereDate?.Year;
        item.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(season.Id));

        result.Item = item;
        result.HasMetadata = true;
        return result;
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => await _client.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);

    internal static async Task<TvMazeSeason?> FindSeasonAsync(
        TvMazeClient client,
        IReadOnlyDictionary<string, string>? seriesProviderIds,
        int? seasonNumber,
        CancellationToken ct)
    {
        if (seasonNumber is null or <= 0)
        {
            return null;
        }

        var showId = await TvMazeHelpers.ResolveShowIdAsync(client, seriesProviderIds, null, null, ct).ConfigureAwait(false);
        if (showId is null)
        {
            return null;
        }

        var seasons = await client.GetSeasonsAsync(showId.Value, ct).ConfigureAwait(false);
        return seasons.FirstOrDefault(s => s.Number == seasonNumber);
    }
}
