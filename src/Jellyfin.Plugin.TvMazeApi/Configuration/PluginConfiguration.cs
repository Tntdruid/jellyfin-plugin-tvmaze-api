using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TvMazeApi.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Max API calls per window. TVmaze allows at least 20 per 10 seconds per IP.</summary>
    public int MaxRequestsPerWindow { get; set; } = 20;

    public int WindowSeconds { get; set; } = 10;

    /// <summary>How long API responses are cached in memory. TVmaze's own edge cache is 60 minutes.</summary>
    public int CacheMinutes { get; set; } = 60;

    /// <summary>Retries after HTTP 429 or transient errors before giving up.</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Provide cast, crew, guest stars, directors and writers. Jellyfin takes people from the first
    /// metadata provider that returns any, so turn this off if another provider should supply them.
    /// </summary>
    public bool IncludePeople { get; set; } = true;

    /// <summary>TVmaze accounts linked to Jellyfin users, for marking watched episodes.</summary>
    public TvMazeUserConfig[] Users { get; set; } = [];

    public TvMazeUserConfig? GetUser(Guid userId)
        => Users.FirstOrDefault(u => u.UserId == userId);
}

public class TvMazeUserConfig
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Mark episodes watched in Jellyfin as watched on TVmaze. Off by default.</summary>
    public bool ScrobbleEnabled { get; set; }

    public bool CanScrobble
        => ScrobbleEnabled && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(ApiKey);
}
