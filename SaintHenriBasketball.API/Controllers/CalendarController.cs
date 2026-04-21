using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Calendar;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[RequireFeature(FeatureFlagKeys.CalendarSync)]
public class CalendarController : BaseApiController
{
    private readonly ICalendarSyncService _calendarSyncService;

    public CalendarController(ICalendarSyncService calendarSyncService)
    {
        _calendarSyncService = calendarSyncService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/calendar-feed")]
    [Authorize]
    [ProducesResponseType(typeof(CalendarFeedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarFeedDto>> GetOwnFeed()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var feed = await _calendarSyncService.EnsureTokenAsync(userId.Value, BaseUrl());
            return Ok(feed);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("api/v{version:apiVersion}/users/me/calendar-feed/regenerate")]
    [Authorize]
    [ProducesResponseType(typeof(CalendarFeedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarFeedDto>> Regenerate()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var feed = await _calendarSyncService.RegenerateTokenAsync(userId.Value, BaseUrl());
            return Ok(feed);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("api/v{version:apiVersion}/calendar/{token}.ics")]
    [AllowAnonymous]
    [Produces("text/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIcs(string token)
    {
        var ics = await _calendarSyncService.BuildIcsForTokenAsync(token);
        if (ics is null) return NotFound();

        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        return new ContentResult
        {
            Content = ics,
            ContentType = "text/calendar; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    private string BaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}
