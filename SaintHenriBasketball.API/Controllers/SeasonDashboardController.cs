using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SeasonDashboard;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// One season at a glance: passes sold against the cap, money collected and pending by age,
/// and how each session filled. Hidden (404) while the season-dashboard flag is off.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/seasons/dashboard")]
[Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.SeasonDashboard)]
public class SeasonDashboardController : ControllerBase
{
    private readonly ISeasonDashboardService _service;

    public SeasonDashboardController(ISeasonDashboardService service)
    {
        _service = service;
    }

    /// <summary>The dashboard for one season, or for the season running today when seasonId is left out.</summary>
    /// <response code="204">The club has no seasons yet.</response>
    [HttpGet]
    [ProducesResponseType(typeof(SeasonDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonDashboardDto>> Get([FromQuery] Guid? seasonId)
    {
        try
        {
            var dashboard = await _service.GetAsync(seasonId);
            return dashboard == null ? NoContent() : Ok(dashboard);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
