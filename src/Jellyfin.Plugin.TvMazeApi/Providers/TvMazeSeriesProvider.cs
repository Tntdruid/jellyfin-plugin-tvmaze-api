using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TvMazeApi.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi.Providers;

public class TvMazeSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
{
    private readonly TvMazeClient _client;
    private readonly ILogger<TvMazeSeriesProvider> _logger;

    public TvMazeSeriesProvider(TvMazeClient client, ILogger<TvMazeSeriesProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => Plugin.ProviderName;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var ownId = TvMazeHelpers.ParseInt(searchInfo.GetProviderId(TvMazeHelpers.ProviderKey));
        if (ownId is null && !string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            var results = await _client.SearchShowsAsync(searchInfo.Name, cancellationToken).ConfigureAwait(false);
            return results.Select(r => r.Show).OfType<TvMazeShow>().Select(ToSearchResult).ToList();
        }

        var id = await TvMazeHelpers.ResolveShowIdAsync(_client, searchInfo.ProviderIds, searchInfo.Name, searchInfo.Year, cancellationToken)
            .ConfigureAwait(false);
        var show = id is null ? null : await _client.GetShowAsync(id.Value, cancellationToken).ConfigureAwait(false);
        return show is null ? [] : [ToSearchResult(show)];
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();
        var id = await TvMazeHelpers.ResolveShowIdAsync(_client, info.ProviderIds, info.Name, info.Year, cancellationToken)
            .ConfigureAwait(false);
        if (id is null)
        {
            _logger.LogDebug("No TVmaze match for series {Name}", info.Name);
            return result;
        }

        var show = await _client.GetShowAsync(id.Value, cancellationToken).ConfigureAwait(false);
        if (show is null)
        {
            return result;
        }

        result.Item = MapSeries(show);
        result.HasMetadata = true;

        foreach (var member in await _client.GetCastAsync(show.Id, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(member.Person?.Name))
            {
                continue;
            }

            var person = new PersonInfo
            {
                Name = member.Person.Name.Trim(),
                Role = member.Character?.Name,
                Type = PersonKind.Actor,
                ImageUrl = member.Person.Image?.Original,
            };
            person.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(member.Person.Id));
            result.AddPerson(person);
        }

        return result;
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => await _client.GetImageResponseAsync(url, cancellationToken).ConfigureAwait(false);

    internal static Series MapSeries(TvMazeShow show)
    {
        var series = new Series
        {
            Name = show.Name,
            Overview = TvMazeHelpers.StripHtml(show.Summary),
            Genres = show.Genres,
            CommunityRating = show.Rating?.Average is { } r ? (float)r : null,
            PremiereDate = TvMazeHelpers.ParseDate(show.Premiered),
            EndDate = TvMazeHelpers.ParseDate(show.Ended),
            HomePageUrl = show.OfficialSite,
            AirTime = show.Schedule?.Time,
            Status = show.Status switch
            {
                "Running" => SeriesStatus.Continuing,
                "Ended" => SeriesStatus.Ended,
                "In Development" => SeriesStatus.Unreleased,
                _ => null,
            },
        };

        series.ProductionYear = series.PremiereDate?.Year;
        if ((show.AverageRuntime ?? show.Runtime) is { } runtime)
        {
            series.RunTimeTicks = TimeSpan.FromMinutes(runtime).Ticks;
        }

        series.AirDays = show.Schedule?.Days
            .Select(d => Enum.TryParse<DayOfWeek>(d, true, out var day) ? (DayOfWeek?)day : null)
            .OfType<DayOfWeek>()
            .ToArray() ?? [];

        var studio = show.Network?.Name ?? show.WebChannel?.Name;
        if (!string.IsNullOrWhiteSpace(studio))
        {
            series.AddStudio(studio);
        }

        series.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(show.Id));
        if (show.Externals?.Thetvdb is { } tvdb)
        {
            series.SetProviderId(MetadataProvider.Tvdb, TvMazeHelpers.ToId(tvdb));
        }

        if (!string.IsNullOrWhiteSpace(show.Externals?.Imdb))
        {
            series.SetProviderId(MetadataProvider.Imdb, show.Externals.Imdb);
        }

        if (show.Externals?.Tvrage is { } tvrage)
        {
            series.SetProviderId(MetadataProvider.TvRage, TvMazeHelpers.ToId(tvrage));
        }

        return series;
    }

    private static RemoteSearchResult ToSearchResult(TvMazeShow show)
    {
        var premiere = TvMazeHelpers.ParseDate(show.Premiered);
        var result = new RemoteSearchResult
        {
            Name = show.Name,
            SearchProviderName = Plugin.ProviderName,
            Overview = TvMazeHelpers.StripHtml(show.Summary),
            PremiereDate = premiere,
            ProductionYear = premiere?.Year,
            ImageUrl = show.Image?.Medium ?? show.Image?.Original,
        };
        result.SetProviderId(TvMazeHelpers.ProviderKey, TvMazeHelpers.ToId(show.Id));
        if (show.Externals?.Thetvdb is { } tvdb)
        {
            result.SetProviderId(MetadataProvider.Tvdb, TvMazeHelpers.ToId(tvdb));
        }

        if (!string.IsNullOrWhiteSpace(show.Externals?.Imdb))
        {
            result.SetProviderId(MetadataProvider.Imdb, show.Externals.Imdb);
        }

        return result;
    }
}
