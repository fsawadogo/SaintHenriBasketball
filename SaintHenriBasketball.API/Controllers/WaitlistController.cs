using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Waitlist;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _waitlistService;
    private readonly ILogger<WaitlistController> _logger;

    public WaitlistController(IWaitlistService waitlistService, ILogger<WaitlistController> logger)
    {
        _waitlistService = waitlistService;
        _logger = logger;
    }

    [HttpPost("join")]
    [ProducesResponseType(typeof(WaitlistDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WaitlistDto>> JoinWaitlist([FromBody] JoinWaitlistDto request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var entry = await _waitlistService.JoinWaitlistAsync(Guid.Parse(userId), request);
            return CreatedAtAction(nameof(GetMyWaitlistEntry), new { sessionId = request.SessionId }, entry);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining waitlist");
            return StatusCode(500, "Failed to join waitlist");
        }
    }

    [HttpDelete("leave/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LeaveWaitlist(Guid sessionId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _waitlistService.LeaveWaitlistAsync(Guid.Parse(userId), sessionId);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(typeof(IReadOnlyList<WaitlistDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WaitlistDto>>> GetSessionWaitlist(Guid sessionId)
    {
        var entries = await _waitlistService.GetSessionWaitlistAsync(sessionId);
        return Ok(entries);
    }

    [HttpGet("me/{sessionId}")]
    [ProducesResponseType(typeof(WaitlistDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WaitlistDto?>> GetMyWaitlistEntry(Guid sessionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var entry = await _waitlistService.GetUserWaitlistEntryAsync(Guid.Parse(userId), sessionId);
        return Ok(entry);
    }
}
