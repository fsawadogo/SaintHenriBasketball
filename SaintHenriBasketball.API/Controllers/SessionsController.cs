using SaintHenriBasketball.API.Controllers;
using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class SessionsController : BaseApiController
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new session (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDto>> CreateSession([FromBody] CreateSessionDto createSessionDto)
    {
        try
        {
            var result = await _sessionService.CreateSessionAsync(createSessionDto);
            return CreatedAtAction(nameof(GetSession), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return StatusCode(500, "An error occurred while creating the session");
        }
    }

    /// <summary>
    /// Get a specific session by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDetailDto>> GetSession(Guid id)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(id);
            return Ok(session);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId}", id);
            return StatusCode(500, "An error occurred while retrieving the session");
        }
    }

    /// <summary>
    /// Get upcoming sessions
    /// </summary>
    [HttpGet]
    [ResponseCache(CacheProfileName = "Default30")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetUpcomingSessions()
    {
        try
        {
            var sessions = await _sessionService.GetUpcomingSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming sessions");
            return StatusCode(500, "An error occurred while retrieving upcoming sessions");
        }
    }

    /// <summary>
    /// Get available sessions
    /// </summary>
    [HttpGet("available")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetAvailableSessions()
    {
        try
        {
            var sessions = await _sessionService.GetAvailableSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available sessions");
            return StatusCode(500, "An error occurred while retrieving available sessions");
        }
    }

    /// <summary>
    /// Register current user for a session
    /// </summary>
    [HttpPost("{sessionId}/register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionRegistrationResponseDto>> RegisterForSession(Guid sessionId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var result = await _sessionService.RegisterForSessionAsync(sessionId, Guid.Parse(userId));
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while registering for the session");
        }
    }

    /// <summary>
    /// Unregister current user from a session
    /// </summary>
    [HttpDelete("{sessionId}/register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnregisterFromSession(Guid sessionId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _sessionService.UnregisterFromSessionAsync(sessionId, Guid.Parse(userId));
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering from session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while unregistering from the session");
        }
    }

    /// <summary>
    /// Cancel a session (Admin only)
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSession(Guid id)
    {
        try
        {
            await _sessionService.CancelSessionAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling session {SessionId}", id);
            return StatusCode(500, "An error occurred while cancelling the session");
        }
    }

    /// <summary>
    /// Update a session (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpdateSessionDto updateSessionDto)
    {
        try
        {
            if (!Enum.IsDefined(typeof(SessionStatus), updateSessionDto.Status))
            {
                return BadRequest("Invalid session status");
            }

            await _sessionService.UpdateSessionAsync(id, updateSessionDto);
            return NoContent();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Session update validation failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Session not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session {SessionId}", id);
            return StatusCode(500, "An error occurred while updating the session");
        }
    }

    /// <summary>
    /// Get all sessions regardless of their status
    /// </summary>
    [HttpGet("all")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetAllSessions()
    {
        try
        {
            var sessions = await _sessionService.GetAllSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all sessions");
            return StatusCode(500, "An error occurred while retrieving all sessions");
        }
    }
}