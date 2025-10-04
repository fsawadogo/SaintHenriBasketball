using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly IAttendanceService _attendanceService;
    private readonly IEmailService _emailService;
    private readonly ISessionService _sessionService;
    private readonly IUserService _userService;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        IEmailService emailService,
        ISessionService sessionService,
        IUserService userService,
        ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService;
        _emailService = emailService;
        _sessionService = sessionService;
        _userService = userService;
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

    /// <summary>
    /// Confirm attendance from email link
    /// </summary>
    [HttpGet("confirm")]
    [AllowAnonymous] // Allow non-authenticated users to confirm via email link
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmAttendanceFromEmail(
        [FromQuery] Guid sessionId,
        [FromQuery] Guid userId,
        [FromQuery] bool attending)
    {
        try
        {
            // Validate session still exists and is upcoming
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound("Session not found");
            }

            if (session.SessionDate < DateTime.UtcNow)
            {
                return BadRequest("Cannot confirm attendance for a past session");
            }

            // Check if user exists
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check if attendance is already marked
            var existingAttendance = await _attendanceService.UpdateAttendanceAsync(sessionId, userId, attending, null, "Updated via email link");

            AttendanceResponseDto response;
            string message;

            if (existingAttendance != null)
            {
                // Update existing attendance
                response = await _attendanceService.UpdateAttendanceAsync(
                    sessionId,
                    userId,
                    attending,
                    null, // No notes when confirming from email
                    "Updated via email link"
                );

                message = attending
                    ? "Your attendance has been confirmed. We look forward to seeing you at the session!"
                    : "You have been marked as not attending this session. We hope to see you at a future session!";
            }
            else
            {
                // Create new attendance record
                response = await _attendanceService.MarkAttendanceAsync(
                    sessionId,
                    userId,
                    attending,
                    null // No notes when confirming from email
                );

                message = attending
                    ? "Thank you for confirming your attendance. We look forward to seeing you at the session!"
                    : "You have been marked as not attending this session. We hope to see you at a future session!";
            }

            // Instead of returning a raw message, let's redirect to a confirmation page on the frontend
            var redirectUrl = $"https://sainthenribasketball.com/attendance/confirmation?status=success&attending={attending}";
            return Redirect(redirectUrl);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Attendance confirmation failed for session {SessionId}", sessionId);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Session or user not found for attendance confirmation {SessionId}, {UserId}", sessionId, userId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming attendance for session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while confirming attendance");
        }
    }

    /// <summary>
    /// Add multiple participants to a session (Admin only)
    /// </summary>
    [HttpPost("sessions/{sessionId}/add-participants")]
    [ProducesResponseType(typeof(AddParticipantsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AddParticipantsResponseDto>> AddParticipantsToSession(
        Guid sessionId,
        [FromBody] AddParticipantsRequest request)
    {
        try
        {
            // Check if user is authenticated
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User must be authenticated to add participants");
            }

            // TODO: Add admin role check here when role-based authorization is implemented
            // For now, we'll allow any authenticated user to add participants
            // This should be changed to require admin role in production

            var response = await _attendanceService.AddParticipantsToSessionAsync(sessionId, request);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed when adding participants to session {SessionId}", sessionId);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found when adding participants {SessionId}", sessionId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding participants to session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while adding participants");
        }
    }

    /// <summary>
    /// Remove multiple participants from a session (Admin only)
    /// </summary>
    [HttpPost("sessions/{sessionId}/remove-participants")]
    [ProducesResponseType(typeof(RemoveParticipantsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RemoveParticipantsResponseDto>> RemoveParticipantsFromSession(
        Guid sessionId,
        [FromBody] RemoveParticipantsRequest request)
    {
        try
        {
            // Check if user is authenticated
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User must be authenticated to remove participants");
            }

            // TODO: Add admin role check here when role-based authorization is implemented
            // For now, we'll allow any authenticated user to remove participants
            // This should be changed to require admin role in production

            var response = await _attendanceService.RemoveParticipantsFromSessionAsync(sessionId, request);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed when removing participants from session {SessionId}", sessionId);
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Session not found when removing participants {SessionId}", sessionId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing participants from session {SessionId}", sessionId);
            return StatusCode(500, "An unexpected error occurred while removing participants");
        }
    }
}