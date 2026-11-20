using Jellyfin.Plugin.TvMazeApi.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TvMazeApi;

/// <summary>TVmaze metadata plugin that respects the TVmaze API rate limit.</summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string ProviderName = "TVmaze API";

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => ProviderName;

    public override Guid Id => Guid.Parse("6f2b3c1e-8a4d-4f7b-9c2e-1d5a7b9e3f40");

    public override string Description => "Fetches series, season and episode metadata from TVmaze, with built-in rate limiting and caching.";

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
        },
    ];
}
