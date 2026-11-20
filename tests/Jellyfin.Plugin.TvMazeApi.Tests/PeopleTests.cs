using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Providers;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.TvMazeApi.Tests;

public class PeopleTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Creator", PersonKind.Creator)]
    [InlineData("Executive Producer", PersonKind.Producer)]
    [InlineData("Co-Producer", PersonKind.Producer)]
    [InlineData("Director", PersonKind.Director)]
    [InlineData("Writer", PersonKind.Writer)]
    [InlineData("Story", PersonKind.Writer)]
    [InlineData("Teleplay", PersonKind.Writer)]
    [InlineData("Assistant To Producers", null)]
    [InlineData("Casting Director", null)]
    [InlineData(null, null)]
    public void MapsCrewTypes(string? type, PersonKind? expected)
        => Assert.Equal(expected, TvMazePeople.CrewKind(type));

    [Fact]
    public void MapsSeriesCrewWithRoleAndId()
    {
        var crew = new[]
        {
            new TvMazeCrewMember { Type = "Creator", Person = new TvMazePerson { Id = 9, Name = " Brian K. Vaughan " } },
            new TvMazeCrewMember { Type = "Assistant To Producers", Person = new TvMazePerson { Id = 10, Name = "Someone" } },
        };

        var person = Assert.Single(TvMazePeople.FromCrew(crew));

        Assert.Equal("Brian K. Vaughan", person.Name);
        Assert.Equal(PersonKind.Creator, person.Type);
        Assert.Equal("Creator", person.Role);
        Assert.Equal("9", person.GetProviderId(MetadataProvider.TvMaze));
    }

    [Fact]
    public void MapsEpisodeGuestCastAndCrewFromEmbeddedJson()
    {
        const string json = """
            {"id":1,"season":1,"number":1,"_embedded":{
              "guestcast":[{"person":{"id":5,"name":"Mackenzie Lintz","image":{"original":"p.jpg"}},"character":{"id":1,"name":"Norrie"}}],
              "guestcrew":[{"guestCrewType":"Story","person":{"id":6,"name":"Stephen King"}},
                           {"guestCrewType":"Director","person":{"id":7,"name":"Niels Arden Oplev"}}]}}
            """;
        var episode = JsonSerializer.Deserialize<TvMazeEpisode>(json, Web)!;

        var people = TvMazePeople.FromEpisode(episode).ToList();

        Assert.Equal(3, people.Count);
        Assert.Equal((PersonKind.GuestStar, "Norrie", "p.jpg"), (people[0].Type, people[0].Role, people[0].ImageUrl));
        Assert.Equal(PersonKind.Writer, people[1].Type);
        Assert.Equal(PersonKind.Director, people[2].Type);
        Assert.Equal("7", people[2].GetProviderId(MetadataProvider.TvMaze));
    }

    [Fact]
    public void EpisodeWithoutEmbeddedPeopleGivesNone()
        => Assert.Empty(TvMazePeople.FromEpisode(new TvMazeEpisode { Id = 1 }));
}
