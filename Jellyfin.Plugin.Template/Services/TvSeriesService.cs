using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.WatchWheel.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.WatchWheel.Services;

/// <summary>
/// Provides television-series watch progress information.
/// </summary>
public class TvSeriesService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvSeriesService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="userDataManager">Jellyfin user data manager.</param>
    public TvSeriesService(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
    }

    /// <summary>
    /// Gets progress across regular episodes for one series.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="seriesId">The Jellyfin series identifier.</param>
    /// <returns>The user's watch progress for the series.</returns>
    public TvSeriesProgress GetProgress(User user, Guid seriesId)
    {
        return GetProgressForSeries(user, new[] { seriesId })[seriesId];
    }

    /// <summary>
    /// Gets regular-episode progress for several series with one episode query.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="seriesIds">The series identifiers to inspect.</param>
    /// <returns>Progress keyed by series identifier, including empty series.</returns>
    public IReadOnlyDictionary<Guid, TvSeriesProgress> GetProgressForSeries(
        User user,
        IEnumerable<Guid> seriesIds)
    {
        var progressBySeries = seriesIds
            .Distinct()
            .ToDictionary(id => id, _ => new TvSeriesProgress());

        if (progressBySeries.Count == 0)
        {
            return progressBySeries;
        }

        // Keep played episodes: they establish that an unfinished series was started.
        var query = new InternalItemsQuery(user)
        {
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Episode],
            EnableTotalRecordCount = false
        };

        // A single filtered series can use the existing narrowly scoped query.
        if (progressBySeries.Count == 1)
        {
            query.ParentId = progressBySeries.Keys.First();
        }

        var nextBySeries = new Dictionary<Guid, Episode>();
        foreach (var episode in _libraryManager.GetItemList(query).OfType<Episode>())
        {
            if (episode.ParentIndexNumber == 0)
            {
                continue;
            }

            var seriesId = episode.SeriesId;
            if (!progressBySeries.TryGetValue(seriesId, out var progress))
            {
                seriesId = episode.FindSeriesId();
                if (!progressBySeries.TryGetValue(seriesId, out progress))
                {
                    continue;
                }
            }

            var userData = _userDataManager.GetUserData(user, episode);
            var played = userData?.Played ?? false;
            var position = Math.Max(0L, userData?.PlaybackPositionTicks ?? 0L);
            progress.HasStarted |= played || position > 0;

            if (played)
            {
                continue;
            }

            progress.RemainingEpisodes++;
            if (nextBySeries.TryGetValue(seriesId, out var currentNext)
                && CompareEpisodes(episode, currentNext) >= 0)
            {
                continue;
            }

            nextBySeries[seriesId] = episode;
            progress.NextEpisodeId = episode.Id;
            progress.NextEpisodeName = episode.Name;
            progress.NextSeasonNumber = episode.ParentIndexNumber;
            progress.NextEpisodeNumber = episode.IndexNumber;
            progress.NextEpisodePlaybackPositionTicks = position;
            progress.NextEpisodeRunTimeTicks = episode.RunTimeTicks;
        }

        return progressBySeries;
    }

    private static int CompareEpisodes(Episode left, Episode right)
    {
        var comparison = (left.ParentIndexNumber ?? int.MaxValue)
            .CompareTo(right.ParentIndexNumber ?? int.MaxValue);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = (left.IndexNumber ?? int.MaxValue)
            .CompareTo(right.IndexNumber ?? int.MaxValue);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = StringComparer.CurrentCulture.Compare(left.Name, right.Name);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }
}
