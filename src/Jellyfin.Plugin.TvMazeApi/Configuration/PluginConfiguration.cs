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
}
