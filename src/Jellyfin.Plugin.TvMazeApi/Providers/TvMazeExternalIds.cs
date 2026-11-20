using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

public class TvMazeSeriesExternalId : IExternalId
{
    public string ProviderName => "TVmaze";

    public string Key => TvMazeHelpers.ProviderKey;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    public bool Supports(IHasProviderIds item) => item is Series;
}

public class TvMazeSeasonExternalId : IExternalId
{
    public string ProviderName => "TVmaze";

    public string Key => TvMazeHelpers.ProviderKey;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Season;

    public bool Supports(IHasProviderIds item) => item is Season;
}

public class TvMazeEpisodeExternalId : IExternalId
{
    public string ProviderName => "TVmaze";

    public string Key => TvMazeHelpers.ProviderKey;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Episode;

    public bool Supports(IHasProviderIds item) => item is Episode;
}

/// <summary>Links back to TVmaze (also satisfies the CC BY-SA attribution requirement).</summary>
public class TvMazeExternalUrlProvider : IExternalUrlProvider
{
    public string Name => "TVmaze";

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (!item.TryGetProviderId(TvMazeHelpers.ProviderKey, out var id))
        {
            yield break;
        }

        var kind = item switch
        {
            Series => "shows",
            Season => "seasons",
            Episode => "episodes",
            _ => null,
        };
        if (kind is not null)
        {
            yield return $"https://www.tvmaze.com/{kind}/{id}";
        }
    }
}
