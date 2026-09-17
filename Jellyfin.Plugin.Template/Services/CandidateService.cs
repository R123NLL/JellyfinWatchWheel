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

    /// <summary>
    /// Initializes a new instance of the <see cref="CandidateService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="userDataManager">Jellyfin user data manager.</param>
    public CandidateService(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
    }

    /// <summary>
    /// Gets Watch Wheel candidates for a user.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="filters">The filters to apply.</param>
    /// <returns>The filtered Watch Wheel result.</returns>
    public WatchWheelResult GetCandidates(
        User user,
        WatchWheelFilters filters)
    {
        var movieItems = _libraryManager.GetItemList(
            new InternalItemsQuery(user)
            {
                Recursive = true,
                IsPlayed = false,
                IncludeItemTypes = [BaseItemKind.Movie],
                EnableTotalRecordCount = false
            });

        var seriesItems = _libraryManager.GetItemList(
            new InternalItemsQuery(user)
            {
                Recursive = true,
                IsPlayed = false,
                IncludeItemTypes = [BaseItemKind.Series],
                EnableTotalRecordCount = false
            });

        var movies = movieItems.Select(item =>
            CreateItem(user, item, "Movie"));

        var series = seriesItems.Select(item =>
            CreateItem(user, item, "Series"));

        var items = movies.Concat(series);

        if (!string.IsNullOrWhiteSpace(filters.Type))
        {
            if (filters.Type.Equals(
                    "movie",
                    StringComparison.OrdinalIgnoreCase)
                || filters.Type.Equals(
                    "movies",
                    StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(item =>
                    item.Type == "Movie");
            }
            else if (filters.Type.Equals(
                         "series",
                         StringComparison.OrdinalIgnoreCase)
                     || filters.Type.Equals(
                         "tv",
                         StringComparison.OrdinalIgnoreCase)
                     || filters.Type.Equals(
                         "shows",
                         StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(item =>
                    item.Type == "Series");
            }
        }

        if (!string.IsNullOrWhiteSpace(filters.Genre))
        {
            items = items.Where(item =>
                item.Genres.Any(itemGenre =>
                    itemGenre.Equals(
                        filters.Genre,
                        StringComparison.OrdinalIgnoreCase)));
        }

        if (filters.Decade.HasValue)
        {
            var startYear = filters.Decade.Value;
            var endYear = startYear + 9;

            items = items.Where(item =>
                item.Year.HasValue
                && item.Year.Value >= startYear
                && item.Year.Value <= endYear);
        }

        if (!filters.IncludeInProgress)
        {
            items = items.Where(item =>
                item.PlaybackPositionTicks == 0);
        }

        var result = items
            .OrderBy(item => item.Name)
            .ToArray();

        return new WatchWheelResult
        {
            Count = result.Length,
            Filters = filters,
            Items = result
        };
    }

    private WatchWheelItem CreateItem(
        User user,
        BaseItem item,
        string type)
    {
        var userData =
            _userDataManager.GetUserData(user, item);

        return new WatchWheelItem
        {
            Id = item.Id,
            Name = item.Name,
            Type = type,
            Year = item.ProductionYear,
            Overview = item.Overview,
            CommunityRating = item.CommunityRating,
            Genres = item.Genres,
            Played = userData?.Played ?? false,
            PlaybackPositionTicks =
                userData?.PlaybackPositionTicks ?? 0
        };
    }
}
