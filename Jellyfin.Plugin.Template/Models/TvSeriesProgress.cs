using System;

namespace Jellyfin.Plugin.WatchWheel.Models;

/// <summary>
/// Represents watch progress across regular episodes of a television series.
/// </summary>
public class TvSeriesProgress
{
    /// <summary>
    /// Gets or sets the number of unwatched regular episodes remaining.
    /// </summary>
    public int RemainingEpisodes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any regular episode is played or has playback progress.
    /// </summary>
    public bool HasStarted { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the next unwatched regular episode.
    /// </summary>
    public Guid? NextEpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the name of the next unwatched regular episode.
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

    /// <summary>
    /// Gets or sets the resume position of the selected next episode.
    /// </summary>
    public long NextEpisodePlaybackPositionTicks { get; set; }

    /// <summary>
    /// Gets a value indicating whether unwatched regular episodes remain.
    /// </summary>
    public bool HasUnwatchedEpisodes => RemainingEpisodes > 0;
}
