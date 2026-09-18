using System;
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
    /// Gets progress across regular episodes, excluding season zero specials.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="seriesId">The Jellyfin series identifier.</param>
    /// <returns>The user's watch progress for the series.</returns>
    public TvSeriesProgress GetProgress(User user, Guid seriesId)
    {
        // Include played episodes so a series remains started between episodes.
        var episodes = _libraryManager.GetItemList(
                new InternalItemsQuery(user)
                {
                    ParentId = seriesId,
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Episode],
                    EnableTotalRecordCount = false
                })
            .OfType<Episode>()
            .Where(episode =>
                episode.SeriesId == seriesId
                || episode.FindSeriesId() == seriesId)
            .Where(episode => episode.ParentIndexNumber != 0)
            .OrderBy(episode => episode.ParentIndexNumber ?? int.MaxValue)
            .ThenBy(episode => episode.IndexNumber ?? int.MaxValue)
            .ThenBy(episode => episode.Name)
            .ThenBy(episode => episode.Id)
            .Select(episode => new
            {
                Episode = episode,
                UserData = _userDataManager.GetUserData(user, episode)
            })
            .ToArray();

        var unwatched = episodes
            .Where(entry => !(entry.UserData?.Played ?? false))
            .ToArray();
        var next = unwatched.FirstOrDefault();

        return new TvSeriesProgress
        {
            RemainingEpisodes = unwatched.Length,
            HasStarted = episodes.Any(entry =>
                (entry.UserData?.Played ?? false)
                || (entry.UserData?.PlaybackPositionTicks ?? 0) > 0),
            NextEpisodeId = next?.Episode.Id,
            NextEpisodeName = next?.Episode.Name,
            NextSeasonNumber = next?.Episode.ParentIndexNumber,
            NextEpisodeNumber = next?.Episode.IndexNumber,
            NextEpisodePlaybackPositionTicks = Math.Max(
                0L,
                next?.UserData?.PlaybackPositionTicks ?? 0L)
        };
    }
}
