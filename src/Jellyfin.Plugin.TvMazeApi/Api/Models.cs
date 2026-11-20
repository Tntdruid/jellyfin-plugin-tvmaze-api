namespace Jellyfin.Plugin.TvMazeApi.Api;

#pragma warning disable CS1591
public sealed class TvMazeShow
{
    public int Id { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Language { get; set; }
    public string[] Genres { get; set; } = [];
    public string? Status { get; set; }
    public int? Runtime { get; set; }
    public int? AverageRuntime { get; set; }
    public string? Premiered { get; set; }
    public string? Ended { get; set; }
    public string? OfficialSite { get; set; }
    public TvMazeSchedule? Schedule { get; set; }
    public TvMazeRating? Rating { get; set; }
    public TvMazeNetwork? Network { get; set; }
    public TvMazeNetwork? WebChannel { get; set; }
    public TvMazeExternals? Externals { get; set; }
    public TvMazeImage? Image { get; set; }
    public string? Summary { get; set; }
    public long? Updated { get; set; }
}

public sealed class TvMazeSearchResult
{
    public double Score { get; set; }
    public TvMazeShow? Show { get; set; }
}

public sealed class TvMazeSchedule
{
    public string? Time { get; set; }
    public string[] Days { get; set; } = [];
}

public sealed class TvMazeRating
{
    public double? Average { get; set; }
}

public sealed class TvMazeNetwork
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public TvMazeCountry? Country { get; set; }
}

public sealed class TvMazeCountry
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Timezone { get; set; }
}

public sealed class TvMazeExternals
{
    public int? Tvrage { get; set; }
    public int? Thetvdb { get; set; }
    public string? Imdb { get; set; }
}

public sealed class TvMazeImage
{
    public string? Medium { get; set; }
    public string? Original { get; set; }
}

public sealed class TvMazeSeason
{
    public int Id { get; set; }
    public string? Url { get; set; }
    public int? Number { get; set; }
    public string? Name { get; set; }
    public int? EpisodeOrder { get; set; }
    public string? PremiereDate { get; set; }
    public string? EndDate { get; set; }
    public TvMazeImage? Image { get; set; }
    public string? Summary { get; set; }
}

public sealed class TvMazeEpisode
{
    public int Id { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
    public int Season { get; set; }
    public int? Number { get; set; }
    public string? Type { get; set; }
    public string? Airdate { get; set; }
    public DateTimeOffset? Airstamp { get; set; }
    public int? Runtime { get; set; }
    public TvMazeRating? Rating { get; set; }
    public TvMazeImage? Image { get; set; }
    public string? Summary { get; set; }
}

public sealed class TvMazeCastMember
{
    public TvMazePerson? Person { get; set; }
    public TvMazeCharacter? Character { get; set; }
}

public sealed class TvMazePerson
{
    public int Id { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
    public TvMazeImage? Image { get; set; }
}

public sealed class TvMazeCharacter
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class TvMazeShowImage
{
    public int Id { get; set; }
    public string? Type { get; set; }
    public bool Main { get; set; }
    public TvMazeResolutions? Resolutions { get; set; }
}

public sealed class TvMazeResolutions
{
    public TvMazeResolution? Original { get; set; }
    public TvMazeResolution? Medium { get; set; }
}

public sealed class TvMazeResolution
{
    public string? Url { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
#pragma warning restore CS1591
