using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.CourtAttendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// Court attendance: the phone roster a captain or admin uses to record what happened, and no-show stats.
[ApiVersion("1.0")]
[ApiController]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.CourtAttendance)]
public class CourtAttendanceController(ICourtAttendanceService courtAttendance, IAuditLogService auditLog) : BaseApiController
{
    [HttpGet("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/roster")]
    [ProducesResponseType(typeof(SessionRosterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionRosterDto>> GetRoster(Guid sessionId)
    {
        try { return Ok(await courtAttendance.GetRosterAsync(sessionId)); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/roster/{userId:guid}/outcome")]
    [ProducesResponseType(typeof(RosterPlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RosterPlayerDto>> SetOutcome(Guid sessionId, Guid userId, [FromBody] SetAttendanceOutcomeRequest body)
    {
        try
        {
            var player = await courtAttendance.SetOutcomeAsync(sessionId, userId, body?.Outcome);
            await auditLog.LogAsync("CourtAttendance.OutcomeMarked", "Session", sessionId,
                $"{player.Name} ({player.UserId}) marked {player.Outcome}", User.AuditUserId(), User.AuditUserName());
            return Ok(player);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/roster/walk-ins")]
    [ProducesResponseType(typeof(RosterPlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RosterPlayerDto>> AddWalkIn(Guid sessionId, [FromBody] AddWalkInRequest body)
    {
        try
        {
            var player = await courtAttendance.AddWalkInAsync(sessionId, body?.UserId ?? Guid.Empty);
            await auditLog.LogAsync("CourtAttendance.WalkInAdded", "Session", sessionId,
                $"{player.Name} ({player.UserId}) added as a walk-in", User.AuditUserId(), User.AuditUserName());
            return Ok(player);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/attendance/no-shows")]
    [ProducesResponseType(typeof(NoShowStatsPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NoShowStatsPageDto>> GetNoShows([FromQuery] NoShowStatsQuery query)
    {
        try { return Ok(await courtAttendance.GetNoShowStatsAsync(query)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
