using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SessionRecap;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.SessionRecaps)]
public class SessionRecapsController : BaseApiController
{
    private readonly ISessionRecapService _recapService;

    public SessionRecapsController(ISessionRecapService recapService)
    {
        _recapService = recapService;
    }

    [HttpGet("api/v{version:apiVersion}/sessions/recent-recaps")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionRecapDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionRecapDto>>> GetRecent([FromQuery] int take = 6)
    {
        var items = await _recapService.GetRecentAsync(Math.Clamp(take, 1, 20));
        return Ok(items);
    }

    [HttpGet("api/v{version:apiVersion}/sessions/{sessionId:guid}/recaps")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionRecapDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionRecapDto>>> GetForSession(Guid sessionId)
    {
        var items = await _recapService.GetBySessionAsync(sessionId);
        return Ok(items);
    }

    [HttpPost("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/recaps")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SessionRecapDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionRecapDto>> Create(Guid sessionId, [FromBody] CreateSessionRecapDto body)
    {
        var adminId = GetUserId();
        if (adminId is null) return Unauthorized();

        try
        {
            var recap = await _recapService.CreateAsync(sessionId, body.PhotoUrl, body.Caption, adminId.Value);
            return CreatedAtAction(nameof(GetForSession), new { sessionId, version = "1.0" }, recap);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("api/v{version:apiVersion}/admin/sessions/{sessionId:guid}/recaps/{recapId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid sessionId, Guid recapId)
    {
        _ = sessionId; // included for URL structure consistency
        try
        {
            await _recapService.DeleteAsync(recapId);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
