using Jellyfin.Plugin.WatchWheel.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WatchWheel;

/// <summary>
/// Registers Watch Wheel services with Jellyfin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<CandidateService>();
        serviceCollection.AddTransient<FilterService>();
        serviceCollection.AddTransient<TvSeriesService>();
    }
}
