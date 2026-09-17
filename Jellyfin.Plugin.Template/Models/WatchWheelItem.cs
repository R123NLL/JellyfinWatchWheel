using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WatchWheel.Models;

/// <summary>
/// Represents a movie or television series available to the Watch Wheel.
/// </summary>
public class WatchWheelItem
{
    /// <summary>
    /// Gets or sets the Jellyfin item identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the community rating.
    /// </summary>
    public float? CommunityRating { get; set; }

    /// <summary>
    /// Gets or sets the genres.
    /// </summary>
    public IReadOnlyList<string> Genres { get; set; } =
        Array.Empty<string>();

    /// <summary>
    /// Gets or sets a value indicating whether the item has been played.
    /// </summary>
    public bool Played { get; set; }

    /// <summary>
    /// Gets or sets the current playback position.
    /// </summary>
    public long PlaybackPositionTicks { get; set; }
}
