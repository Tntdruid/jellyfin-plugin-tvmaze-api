using Jellyfin.Plugin.TvMazeApi.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class ExternalIdTests
{
    // Regression: the TVmaze id was saved on people but no field was shown on person pages.
    [Fact]
    public void PersonHasTvMazeExternalId()
    {
        var externalId = new TvMazePersonExternalId();

        Assert.True(externalId.Supports(new Person()));
        Assert.False(externalId.Supports(new Series()));
        Assert.Equal("TvMaze", externalId.Key);
    }

    [Fact]
    public void PersonLinksToTvMazePeoplePage()
    {
        var person = new Person();
        person.SetProviderId(MetadataProvider.TvMaze, "12345");

        Assert.Equal(["https://www.tvmaze.com/people/12345"], new TvMazeExternalUrlProvider().GetExternalUrls(person));
    }

    [Fact]
    public void SeriesSeasonEpisodeLinks()
    {
        var provider = new TvMazeExternalUrlProvider();
        BaseItem Item(BaseItem item)
        {
            item.SetProviderId(MetadataProvider.TvMaze, "7");
            return item;
        }

        Assert.Equal(["https://www.tvmaze.com/shows/7"], provider.GetExternalUrls(Item(new Series())));
        Assert.Equal(["https://www.tvmaze.com/seasons/7"], provider.GetExternalUrls(Item(new Season())));
        Assert.Equal(["https://www.tvmaze.com/episodes/7"], provider.GetExternalUrls(Item(new Episode())));
    }
}
