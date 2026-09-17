using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WatchWheel.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WatchWheel;

/// <summary>
/// Jellyfin Watch Wheel plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin XML serializer.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Watch Wheel";

    /// <inheritdoc />
    public override string Description =>
        "Spin a wheel to choose something unwatched from your Jellyfin library.";

    /// <inheritdoc />
    public override Guid Id =>
        Guid.Parse("4c6f5316-58f6-4d90-87cc-b8ca47b40a01");

    /// <summary>
    /// Gets the current Watch Wheel plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var namespaceName =
            GetType().Namespace ?? "Jellyfin.Plugin.WatchWheel";

        return
        [
            // Existing settings page.
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath =
                    namespaceName + ".Configuration.configPage.html"
            },

            // Main Watch Wheel UI.
            new PluginPageInfo
            {
                Name = "WatchWheelPage",
                DisplayName = "Watch Wheel",
                EmbeddedResourcePath =
                    namespaceName + ".Web.watchWheel.html",
                EnableInMainMenu = true
            },

            // JavaScript asset.
            new PluginPageInfo
            {
                Name = "watchWheel.js",
                EmbeddedResourcePath =
                    namespaceName + ".Web.watchWheel.js"
            },

            // CSS asset.
            new PluginPageInfo
            {
                Name = "watchWheel.css",
                EmbeddedResourcePath =
                    namespaceName + ".Web.watchWheel.css"
            }
        ];
    }
}
