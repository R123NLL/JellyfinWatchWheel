using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WatchWheel.Models;

/// <summary>
/// Represents the result returned by the Watch Wheel candidate service.
/// </summary>
public class WatchWheelResult
{
    /// <summary>
    /// Gets or sets the number of matching candidates.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the filters used for the request.
    /// </summary>
    public WatchWheelFilters Filters { get; set; } = new();

    /// <summary>
    /// Gets or sets the matching Watch Wheel items.
    /// </summary>
    public IReadOnlyList<WatchWheelItem> Items { get; set; } =
        Array.Empty<WatchWheelItem>();
}
