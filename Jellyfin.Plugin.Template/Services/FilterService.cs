using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.WatchWheel.Services;

/// <summary>
/// Provides available Watch Wheel filter options for a Jellyfin user.
/// </summary>
public class FilterService
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    public FilterService(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the available filter options for the supplied user.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <returns>Available media types, genres, and decades.</returns>
    public object GetAvailableFilters(User user)
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

        var allItems = movieItems
            .Concat(seriesItems)
            .ToArray();

        var genres = allItems
            .SelectMany(item => item.Genres ?? Array.Empty<string>())
            .Where(itemGenre => !string.IsNullOrWhiteSpace(itemGenre))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(itemGenre => itemGenre)
            .ToArray();

        var decades = allItems
            .Where(item => item.ProductionYear.HasValue)
            .Select(item => (item.ProductionYear!.Value / 10) * 10)
            .Distinct()
            .OrderBy(decade => decade)
            .ToArray();

        return new
        {
            Types = new[]
            {
                "both",
                "movie",
                "series"
            },
            Genres = genres,
            Decades = decades
        };
    }
}
