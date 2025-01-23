using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly IAttendanceService _attendanceService;
    private readonly IEmailService _emailService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        IEmailService emailService,
        ISessionService sessionService,
        ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService;
        _emailService = emailService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Mark user's attendance for a session
    /// </summary>
    [HttpPost("sessions/{sessionId}")]
    [ProducesResponseType(typeof(AttendanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceResponseDto>> MarkAttendance(
        Guid sessionId,
        [FromBody] MarkAttendanceRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var response = await _attendanceService.MarkAttendanceAsync(
                sessionId,
                Guid.Parse(userId),
                request.IsAttending,
                request.Notes);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Attendance marking failed for session {SessionId}", sessionId);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found {SessionId}", sessionId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking attendance for session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while marking attendance");
        }
    }

    /// <summary>
    /// Update attendance for a session
    /// </summary>
    [HttpPut("sessions/{sessionId}")]
    [ProducesResponseType(typeof(AttendanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceResponseDto>> UpdateAttendance(
        Guid sessionId,
        [FromBody] UpdateAttendanceRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var response = await _attendanceService.UpdateAttendanceAsync(
                sessionId,
                Guid.Parse(userId),
                request.IsAttending,
                request.Notes,
                request.UpdateReason);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Attendance update failed for session {SessionId}", sessionId);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Attendance record not found for session {SessionId}", sessionId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attendance for session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while updating attendance");
        }
    }

    /// <summary>
    /// Get my attendance history
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<AttendanceResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AttendanceResponseDto>>> GetMyAttendanceHistory()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var history = await _attendanceService.GetUserAttendanceHistoryAsync(Guid.Parse(userId));
        return Ok(history);
    }

    /// <summary>
    /// Get session attendance summary
    /// </summary>
    [HttpGet("sessions/{sessionId}/summary")]
    [ProducesResponseType(typeof(SessionAttendanceSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionAttendanceSummaryDto>> GetSessionAttendanceSummary(Guid sessionId)
    {
        try
        {
            var summary = await _attendanceService.GetSessionAttendanceSummaryAsync(sessionId);
            return Ok(summary);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
    
    /// <summary>
    /// Get list of registered users for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/users")]
    [ProducesResponseType(typeof(IEnumerable<AttendanceUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AttendanceUserDto>>> GetSessionAttendees(Guid sessionId)
    {
        try
        {
            var attendees = await _attendanceService.GetSessionAttendeesAsync(sessionId);
            return Ok(attendees);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found {SessionId}", sessionId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendees for session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while retrieving session attendees");
        }
    }
}