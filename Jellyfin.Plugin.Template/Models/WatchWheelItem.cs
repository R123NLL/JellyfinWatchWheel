using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.WatchWheel.Models;

/// <summary>
/// Represents a movie or television series available to the Watch Wheel.
/// </summary>
public class WatchWheelItem
{
    /// <summary>
    /// Gets or sets the movie or series identifier used for details and artwork.
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
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets a value indicating whether the item has been played.
    /// </summary>
    public bool Played { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the unwatched movie or unfinished series has been started.
    /// </summary>
    public bool IsInProgress { get; set; }

    /// <summary>
    /// Gets or sets the resume position of the movie or selected next episode.
    /// </summary>
    public long PlaybackPositionTicks { get; set; }

    /// <summary>
    /// Gets or sets the full runtime of the movie or selected next episode.
    /// </summary>
    public long? RunTimeTicks { get; set; }

    /// <summary>
    /// Gets or sets the number of unwatched regular episodes; applies only to series.
    /// </summary>
    public int? RemainingEpisodes { get; set; }

    /// <summary>
    /// Gets or sets the next unwatched episode identifier; applies only to series.
    /// </summary>
    public Guid? NextEpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the next unwatched episode name; applies only to series.
    /// </summary>
    public string? NextEpisodeName { get; set; }

    /// <summary>
    /// Gets or sets the season number of the next episode.
    /// </summary>
    public int? NextSeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode number of the next episode.
    /// </summary>
    public int? NextEpisodeNumber { get; set; }
}
