using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.VolunteerRoles;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// What a court captain (or admin) needs around the court roster: a session to pick and a walk-in player search
/// that exposes only names and emails of active players, not the full admin directory.
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/court")]
[Authorize(Policy = StaffAccess.CourtCaptainOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.CourtAttendance)]
public class CourtCaptainController(ICourtCaptainService court) : BaseApiController
{
    /// <summary>Sessions from 7 days ago to 14 days ahead, not cancelled, by date and start time.</summary>
    [HttpGet("sessions")]
    [RequireFeature(FeatureFlagKeys.VolunteerRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<CourtSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourtSessionDto>>> GetSessions() =>
        Ok(await court.GetSessionsAsync());

    /// <summary>Active players whose name, email or username contains the search (at least 2 characters), at most 20.</summary>
    [HttpGet("players")]
    [ProducesResponseType(typeof(IReadOnlyList<CourtPlayerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourtPlayerDto>>> SearchPlayers([FromQuery] string? search) =>
        Ok(await court.SearchPlayersAsync(search));
}
