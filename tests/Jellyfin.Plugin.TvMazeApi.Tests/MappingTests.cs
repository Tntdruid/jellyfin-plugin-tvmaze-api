using System.Text.Json;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Providers;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class MappingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string ShowJson = """
        {"id":1,"url":"https://www.tvmaze.com/shows/1/under-the-dome","name":"Under the Dome","type":"Scripted",
         "language":"English","genres":["Drama","Science-Fiction","Thriller"],"status":"Ended","runtime":60,
         "averageRuntime":60,"premiered":"2013-06-24","ended":"2015-09-10","officialSite":"http://www.cbs.com/shows/under-the-dome/",
         "schedule":{"time":"22:00","days":["Thursday"]},"rating":{"average":6.5},
         "network":{"id":2,"name":"CBS","country":{"name":"United States","code":"US","timezone":"America/New_York"}},
         "webChannel":null,"externals":{"tvrage":25988,"thetvdb":264492,"imdb":"tt1553656"},
         "image":{"medium":"m.jpg","original":"o.jpg"},
         "summary":"<p><b>Under the Dome</b> is the story &amp; more.</p><p>Second.</p>","updated":1704794065}
        """;

    [Fact]
    public void MapsSeries()
    {
        var show = JsonSerializer.Deserialize<TvMazeShow>(ShowJson, Web)!;
        var series = TvMazeSeriesProvider.MapSeries(show);

        Assert.Equal("Under the Dome", series.Name);
        Assert.Equal("Under the Dome is the story & more.\n\nSecond.", series.Overview);
        Assert.Equal(["Drama", "Science-Fiction", "Thriller"], series.Genres);
        Assert.Equal(SeriesStatus.Ended, series.Status);
        Assert.Equal(2013, series.ProductionYear);
        Assert.Equal([DayOfWeek.Thursday], series.AirDays);
        Assert.Equal(["CBS"], series.Studios);
        Assert.Equal("1", series.GetProviderId(MetadataProvider.TvMaze));
        Assert.Equal("264492", series.GetProviderId(MetadataProvider.Tvdb));
        Assert.Equal("tt1553656", series.GetProviderId(MetadataProvider.Imdb));
        Assert.Equal(6.5f, series.CommunityRating);
    }

    [Fact]
    public void PickBestMatchPrefersNameAndYear()
    {
        TvMazeSearchResult R(int id, string name, string premiered) => new() { Show = new TvMazeShow { Id = id, Name = name, Premiered = premiered } };
        var results = new[] { R(1, "Battlestar Galactica", "1978-09-17"), R(2, "Battlestar Galactica", "2004-10-18"), R(3, "Other", "2004-01-01") };

        Assert.Equal(2, TvMazeHelpers.PickBestMatch(results, "Battlestar Galactica", 2004)!.Id);
        Assert.Equal(1, TvMazeHelpers.PickBestMatch(results, "Battlestar Galactica", null)!.Id);
        Assert.Equal(1, TvMazeHelpers.PickBestMatch(results, "battlestar: galactica", 1999)!.Id);
    }

    [Fact]
    public void MatchesEpisodesBySeasonNumberSpecialsAndDate()
    {
        var episodes = new[]
        {
            new TvMazeEpisode { Id = 1, Season = 1, Number = 1, Airdate = "2020-01-01" },
            new TvMazeEpisode { Id = 2, Season = 1, Number = null, Airdate = "2020-01-05", Type = "significant_special" },
            new TvMazeEpisode { Id = 3, Season = 1, Number = 2, Airdate = "2020-01-08" },
            new TvMazeEpisode { Id = 4, Season = 2, Number = null, Airdate = "2021-01-05", Type = "significant_special" },
        };

        Assert.Equal(3, TvMazeEpisodeProvider.MatchEpisode(episodes, 1, 2, null)!.Id);
        Assert.Equal(2, TvMazeEpisodeProvider.MatchEpisode(episodes, 0, 1, null)!.Id);
        Assert.Equal(4, TvMazeEpisodeProvider.MatchEpisode(episodes, 0, 2, null)!.Id);
        Assert.Equal(3, TvMazeEpisodeProvider.MatchEpisode(episodes, null, null, new DateTime(2020, 1, 8))!.Id);
        Assert.Null(TvMazeEpisodeProvider.MatchEpisode(episodes, 5, 1, null));
    }

    [Fact]
    public void MapsShowImagesMainFirst()
    {
        var images = new[]
        {
            new TvMazeShowImage { Type = "poster", Main = false, Resolutions = new() { Original = new() { Url = "p2" } } },
            new TvMazeShowImage { Type = "poster", Main = true, Resolutions = new() { Original = new() { Url = "p1", Width = 680, Height = 1000 } } },
            new TvMazeShowImage { Type = "background", Resolutions = new() { Original = new() { Url = "bg" } } },
            new TvMazeShowImage { Type = null, Resolutions = new() { Original = new() { Url = "x" } } },
        };

        var mapped = TvMazeSeriesImageProvider.MapImages(images);

        Assert.Equal(["p1", "p2", "bg"], mapped.Select(i => i.Url));
        Assert.Equal(ImageType.Backdrop, mapped[2].Type);
    }
}
