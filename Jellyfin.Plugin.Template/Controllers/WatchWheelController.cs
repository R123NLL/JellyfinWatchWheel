using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.WatchWheel.Controllers;

/// <summary>
/// Provides API endpoints for the Watch Wheel plugin.
/// </summary>
[ApiController]
[Route("WatchWheel")]
[Authorize]
public class WatchWheelController : ControllerBase
{
    private readonly IAuthorizationContext _authorizationContext;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchWheelController"/> class.
    /// </summary>
    /// <param name="authorizationContext">Jellyfin authorization context.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="userDataManager">Jellyfin user data manager.</param>
    public WatchWheelController(
        IAuthorizationContext authorizationContext,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager)
    {
        _authorizationContext = authorizationContext;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
    }

    /// <summary>
    /// Gets the current Watch Wheel plugin status.
    /// </summary>
    /// <returns>Basic plugin status information.</returns>
    [HttpGet("Status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Plugin = "Watch Wheel",
            Version = "1.0.0",
            Status = "ready"
        });
    }

    /// <summary>
    /// Gets information about the currently authenticated Jellyfin user.
    /// </summary>
    /// <returns>The current Jellyfin user.</returns>
    [HttpGet("User")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var authorizationInfo =
            await _authorizationContext
                .GetAuthorizationInfo(Request)
                .ConfigureAwait(false);

        var user = authorizationInfo.User;

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            UserId = user.Id,
            Username = user.Username
        });
    }

    /// <summary>
    /// Gets unwatched movies and television series for the current user.
    /// </summary>
    /// <param name="type">
    /// Optional media type filter.
    /// Supported values: movie, series, tv, both.
    /// </param>
    /// <param name="genre">
    /// Optional genre filter.
    /// </param>
    /// <param name="decade">
    /// Optional decade filter, for example 1990 or 2000.
    /// </param>
    /// <param name="includeInProgress">
    /// Whether partially watched items should be included.
    /// </param>
    /// <returns>Watch Wheel candidate items.</returns>
    [HttpGet("Items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? type = null,
        [FromQuery] string? genre = null,
        [FromQuery] int? decade = null,
        [FromQuery] bool includeInProgress = true)
    {
        var authorizationInfo =
            await _authorizationContext
                .GetAuthorizationInfo(Request)
                .ConfigureAwait(false);

        var user = authorizationInfo.User;

        if (user is null)
        {
            return Unauthorized();
        }

        // Query Movies and Series separately.
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
        {
            var userData = _userDataManager.GetUserData(user, item);

            return new
            {
                Id = item.Id,
                item.Name,
                Type = "Movie",
                Year = item.ProductionYear,
                item.Overview,
                item.CommunityRating,
                item.Genres,
                Played = userData?.Played ?? false,
                PlaybackPositionTicks =
                    userData?.PlaybackPositionTicks ?? 0
            };
        });

        var series = seriesItems.Select(item =>
        {
            var userData = _userDataManager.GetUserData(user, item);

            return new
            {
                Id = item.Id,
                item.Name,
                Type = "Series",
                Year = item.ProductionYear,
                item.Overview,
                item.CommunityRating,
                item.Genres,
                Played = userData?.Played ?? false,
                PlaybackPositionTicks =
                    userData?.PlaybackPositionTicks ?? 0
            };
        });

        var items = movies.Concat(series);

        // Media type filter.
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (type.Equals(
                    "movie",
                    StringComparison.OrdinalIgnoreCase)
                || type.Equals(
                    "movies",
                    StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(item => item.Type == "Movie");
            }
            else if (type.Equals(
                         "series",
                         StringComparison.OrdinalIgnoreCase)
                     || type.Equals(
                         "tv",
                         StringComparison.OrdinalIgnoreCase)
                     || type.Equals(
                         "shows",
                         StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(item => item.Type == "Series");
            }

            // "both" does not need filtering.
        }

        // Genre filter.
        if (!string.IsNullOrWhiteSpace(genre))
        {
            items = items.Where(item =>
                item.Genres?.Any(itemGenre =>
                    itemGenre.Equals(
                        genre,
                        StringComparison.OrdinalIgnoreCase)) == true);
        }

        // Decade filter.
        if (decade.HasValue)
        {
            var startYear = decade.Value;
            var endYear = startYear + 9;

            items = items.Where(item =>
                item.Year.HasValue
                && item.Year.Value >= startYear
                && item.Year.Value <= endYear);
        }

        // Remove partially watched items when requested.
        if (!includeInProgress)
        {
            items = items.Where(item =>
                item.PlaybackPositionTicks == 0);
        }

        var result = items
            .OrderBy(item => item.Name)
            .ToArray();

        return Ok(new
        {
            Count = result.Length,
            Filters = new
            {
                Type = type ?? "both",
                Genre = genre,
                Decade = decade,
                IncludeInProgress = includeInProgress
            },
            Items = result
        });
    }

    /// <summary>
    /// Gets the available Watch Wheel filter options for the current user.
    /// </summary>
    /// <returns>Available media types, genres, and decades.</returns>
    [HttpGet("Filters")]
    public async Task<IActionResult> GetFilters()
    {
        var authorizationInfo =
            await _authorizationContext
                .GetAuthorizationInfo(Request)
                .ConfigureAwait(false);

        var user = authorizationInfo.User;

        if (user is null)
        {
            return Unauthorized();
        }

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

        return Ok(new
        {
            Types = new[]
            {
                "both",
                "movie",
                "series"
            },
            Genres = genres,
            Decades = decades
        });
    }
}
