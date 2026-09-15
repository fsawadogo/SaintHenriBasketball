using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.WaitlistAdmin;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// Admin waitlist management: a session's line, demand by week, manual offers, removal and reordering.
/// Every endpoint is admin-only and hidden (404) while the waitlist-admin flag is off.
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/waitlist")]
[ApiController]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.WaitlistAdmin)]
public class WaitlistAdminController : ControllerBase
{
    private readonly IWaitlistAdminService _service;

    public WaitlistAdminController(IWaitlistAdminService service)
    {
        _service = service;
    }

    /// <summary>A session's waitlist in line order, with capacity, bookings and open spots</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(WaitlistSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaitlistSessionDto>> GetSession(Guid sessionId)
    {
        try { return Ok(await _service.GetSessionWaitlistAsync(sessionId)); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Upcoming non-cancelled sessions with capacity, bookings, waitlist, outstanding offers and fill rate</summary>
    [HttpGet("demand")]
    [ProducesResponseType(typeof(WaitlistDemandDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WaitlistDemandDto>> GetDemand([FromQuery] int weeks = 8) =>
        Ok(await _service.GetDemandAsync(weeks));

    /// <summary>Offer a place now to one waiting entry, with the same notifications as automatic promotion</summary>
    [HttpPost("entries/{entryId:guid}/offer")]
    [ProducesResponseType(typeof(WaitlistOfferResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaitlistOfferResultDto>> Offer(Guid entryId)
    {
        try { return Ok(await _service.SendOfferAsync(entryId)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Remove an entry from the waitlist and renumber the rest; the player is not emailed</summary>
    [HttpDelete("entries/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid entryId)
    {
        try { await _service.RemoveEntryAsync(entryId); return NoContent(); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Move an entry to a 1-based position (clamped to the line); returns the updated waitlist</summary>
    [HttpPut("entries/{entryId:guid}/position")]
    [ProducesResponseType(typeof(WaitlistSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WaitlistSessionDto>> Move(Guid entryId, [FromBody] MoveWaitlistEntryDto body)
    {
        try { return Ok(await _service.MoveEntryAsync(entryId, body.Position!.Value)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
