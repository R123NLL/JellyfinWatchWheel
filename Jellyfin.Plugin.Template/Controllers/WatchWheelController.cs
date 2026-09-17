using System.Threading.Tasks;
using Jellyfin.Plugin.WatchWheel.Models;
using Jellyfin.Plugin.WatchWheel.Services;
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
    private readonly CandidateService _candidateService;
    private readonly FilterService _filterService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchWheelController"/> class.
    /// </summary>
    /// <param name="authorizationContext">Jellyfin authorization context.</param>
    /// <param name="candidateService">Watch Wheel candidate service.</param>
    /// <param name="filterService">Watch Wheel filter service.</param>
    public WatchWheelController(
        IAuthorizationContext authorizationContext,
        CandidateService candidateService,
        FilterService filterService)
    {
        _authorizationContext = authorizationContext;
        _candidateService = candidateService;
        _filterService = filterService;
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
    /// <param name="type">Optional media type filter.</param>
    /// <param name="genre">Optional genre filter.</param>
    /// <param name="decade">Optional decade filter.</param>
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

        var filters = new WatchWheelFilters
        {
            Type = type ?? "both",
            Genre = genre,
            Decade = decade,
            IncludeInProgress = includeInProgress
        };

        var result =
            _candidateService.GetCandidates(
                user,
                filters);

        return Ok(result);
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

        return Ok(
            _filterService.GetAvailableFilters(user));
    }
}
