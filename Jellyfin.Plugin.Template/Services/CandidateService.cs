using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.WatchWheel.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.WatchWheel.Services;

/// <summary>
/// Builds and filters Watch Wheel candidates for a Jellyfin user.
/// </summary>
public class CandidateService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly TvSeriesService _tvSeriesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="userDataManager">Jellyfin user data manager.</param>
    /// <param name="tvSeriesService">Television series progress service.</param>
    public CandidateService(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        TvSeriesService tvSeriesService)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _tvSeriesService = tvSeriesService;
    }

    /// <summary>
    /// Gets Watch Wheel candidates for a user.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="filters">The filters to apply.</param>
    /// <returns>The filtered Watch Wheel result.</returns>
    public WatchWheelResult GetCandidates(User user, WatchWheelFilters filters)
    {
        var type = filters.Type?.Trim().ToLowerInvariant();
        var onlyMovies = type is "movie" or "movies";
        var onlySeries = type is "series" or "tv" or "shows";

        var movieItems = onlySeries
            ? Array.Empty<BaseItem>()
            : _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                Recursive = true,
                IsPlayed = false,
                IncludeItemTypes = [BaseItemKind.Movie],
                EnableTotalRecordCount = false
            }).ToArray();

        // Episode history, rather than the series Played flag, determines eligibility.
        var seriesItems = onlyMovies
            ? Array.Empty<BaseItem>()
            : _libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                Recursive = true,
                IncludeItemTypes = [BaseItemKind.Series],
                EnableTotalRecordCount = false
            }).ToArray();

        var movies = movieItems
            .Where(item => MatchesMetadata(item, filters))
            .Select(item => CreateMovieItem(user, item));
        var matchingSeries = seriesItems
            .Where(item => MatchesMetadata(item, filters))
            .ToArray();
        var progressBySeries = _tvSeriesService.GetProgressForSeries(
            user,
            matchingSeries.Select(item => item.Id));
        var series = matchingSeries
            .Select(item => CreateSeriesItem(item, progressBySeries[item.Id]))
            .Where(item => item is not null)
            .Select(item => item!);

        var result = movies.Concat(series)
            .Where(item => filters.IncludeInProgress || !item.IsInProgress)
            .OrderBy(item => item.Name)
            .ToArray();

        return new WatchWheelResult
        {
            Count = result.Length,
            Filters = filters,
            Items = result
        };
    }

    private static bool MatchesMetadata(BaseItem item, WatchWheelFilters filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.Genre)
            && !(item.Genres ?? Array.Empty<string>()).Any(genre =>
                string.Equals(genre, filters.Genre, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (filters.Decade.HasValue)
        {
            var startYear = (long)filters.Decade.Value;
            return item.ProductionYear.HasValue
                && item.ProductionYear.Value >= startYear
                && item.ProductionYear.Value <= startYear + 9;
        }

        return true;
    }

    private static WatchWheelItem? CreateSeriesItem(
        BaseItem item,
        TvSeriesProgress progress)
    {
        if (!progress.HasUnwatchedEpisodes)
        {
            return null;
        }

        return new WatchWheelItem
        {
            Id = item.Id,
            Name = item.Name,
            Type = "Series",
            Year = item.ProductionYear,
            Overview = item.Overview,
            CommunityRating = item.CommunityRating,
            Genres = item.Genres ?? Array.Empty<string>(),
            Played = false,
            IsInProgress = progress.HasStarted,
            PlaybackPositionTicks = progress.NextEpisodePlaybackPositionTicks,
            RemainingEpisodes = progress.RemainingEpisodes,
            NextEpisodeId = progress.NextEpisodeId,
            NextEpisodeName = progress.NextEpisodeName,
            NextSeasonNumber = progress.NextSeasonNumber,
            NextEpisodeNumber = progress.NextEpisodeNumber
        };
    }

    private WatchWheelItem CreateMovieItem(User user, BaseItem item)
    {
        var userData = _userDataManager.GetUserData(user, item);
        var position = Math.Max(0L, userData?.PlaybackPositionTicks ?? 0L);

        return new WatchWheelItem
        {
            Id = item.Id,
            Name = item.Name,
            Type = "Movie",
            Year = item.ProductionYear,
            Overview = item.Overview,
            CommunityRating = item.CommunityRating,
            Genres = item.Genres ?? Array.Empty<string>(),
            Played = userData?.Played ?? false,
            IsInProgress = position > 0,
            PlaybackPositionTicks = position
        };
    }
}
