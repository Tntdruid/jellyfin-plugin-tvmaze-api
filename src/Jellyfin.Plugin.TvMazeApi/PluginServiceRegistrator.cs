using Jellyfin.Plugin.TvMazeApi.Api;
using Jellyfin.Plugin.TvMazeApi.Scrobbling;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TvMazeApi;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(sp => new TvMazeClient(
            TvMazeClient.CreateDefaultHandler(),
            () => Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration(),
            TimeProvider.System,
            sp.GetRequiredService<ILogger<TvMazeClient>>()));
        serviceCollection.AddSingleton<TvMazeScrobbler>();
        serviceCollection.AddHostedService<ScrobbleService>();
    }
}
