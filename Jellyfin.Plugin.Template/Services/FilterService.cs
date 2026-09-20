using System;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.WatchWheel.Models;

namespace Jellyfin.Plugin.WatchWheel.Services;

/// <summary>
/// Provides filter options from eligible Watch Wheel candidates.
/// </summary>
public class FilterService
{
    private readonly CandidateService _candidateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterService"/> class.
    /// </summary>
    /// <param name="candidateService">Watch Wheel candidate service.</param>
    public FilterService(CandidateService candidateService)
    {
        _candidateService = candidateService;
    }

    /// <summary>
    /// Gets global filter options, including eligible in-progress items.
    /// </summary>
    /// <param name="user">The Jellyfin user.</param>
    /// <returns>Available media types, genres, decades, and release years.</returns>
    public object GetAvailableFilters(User user)
    {
        var items = _candidateService.GetCandidates(user, new WatchWheelFilters
        {
            Type = "both",
            IncludeInProgress = true
        }).Items;

        var genres = items
            .SelectMany(item => item.Genres)
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(genre => genre)
            .ToArray();

        var decades = items
            .Where(item => item.Year.HasValue)
            .Select(item => (item.Year!.Value / 10) * 10)
            .Distinct()
            .OrderBy(decade => decade)
            .ToArray();

        var years = items
            .Where(item => item.Year.HasValue)
            .Select(item => item.Year!.Value)
            .Distinct()
            .OrderByDescending(year => year)
            .ToArray();

        return new
        {
            Types = new[] { "both", "movie", "series" },
            Genres = genres,
            Decades = decades,
            Years = years,
            Libraries = _candidateService.GetLibraries(user)
                .Select(folder => new { folder.Id, folder.Name })
                .ToArray()
        };
    }
}
