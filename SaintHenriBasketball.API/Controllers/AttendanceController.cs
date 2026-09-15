using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Attendance;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly SaintHenriBasketball.Application.Helpers.AttendanceLinks _links;
    private readonly IAttendanceService _attendanceService;
    private readonly IEmailService _emailService;
    private readonly ISessionService _sessionService;
    private readonly IUserService _userService;
    private readonly ILogger<AttendanceController> _logger;
    private readonly IAuditLogService _auditLogService;

    public AttendanceController(
        SaintHenriBasketball.Application.Helpers.AttendanceLinks links,
        IAttendanceService attendanceService,
        IEmailService emailService,
        ISessionService sessionService,
        IUserService userService,
        ILogger<AttendanceController> logger,
        IAuditLogService auditLogService)
    {
        _links = links;
        _attendanceService = attendanceService;
        _emailService = emailService;
        _sessionService = sessionService;
        _userService = userService;
        _logger = logger;
        _auditLogService = auditLogService;
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
            // Players see who is coming, not each other's notes.
            if (!User.IsInRole("Admin"))
            {
                foreach (var attendance in summary.Attendances ?? new List<AttendanceResponseDto>())
                {
                    attendance.Notes = null;
                    attendance.UpdateReason = null;
                }
            }
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
    [Authorize(Roles = "Admin")]
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
    /// Get the privacy-safe list of players who confirmed they are attending.
    /// </summary>
    [HttpGet("sessions/{sessionId}/players")]
    [RequireFeature(FeatureFlagKeys.SessionAttendees)]
    [ProducesResponseType(typeof(IEnumerable<SessionPlayerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SessionPlayerDto>>> GetSessionPlayers(Guid sessionId)
    {
        try
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var attendees = await _attendanceService.GetSessionAttendeesAsync(sessionId);
            var players = attendees
                .Where(attendee => attendee.IsAttending)
                .Select(attendee =>
                {
                    var firstName = attendee.FirstName.Trim();
                    var lastName = attendee.LastName.Trim();
                    var lastInitial = string.IsNullOrWhiteSpace(lastName) ? string.Empty : $" {char.ToUpperInvariant(lastName[0])}.";
                    var displayName = string.IsNullOrWhiteSpace(firstName) ? "Player" : $"{firstName}{lastInitial}";
                    var initials = string.Concat(
                        string.IsNullOrWhiteSpace(firstName) ? string.Empty : char.ToUpperInvariant(firstName[0]),
                        string.IsNullOrWhiteSpace(lastName) ? string.Empty : char.ToUpperInvariant(lastName[0]));

                    return new SessionPlayerDto(
                        displayName,
                        string.IsNullOrWhiteSpace(initials) ? "P" : initials,
                        attendee.UserId.ToString().Equals(currentUserId, StringComparison.OrdinalIgnoreCase));
                })
                .OrderBy(player => player.DisplayName)
                .ToList();

            return Ok(players);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    public record SessionPlayerDto(string DisplayName, string Initials, bool IsCurrentUser);

    /// <summary>
    /// Confirm attendance from email link
    /// </summary>
    [HttpGet("confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> PreviewAttendanceLink([FromQuery] string? token)
    {
        var data = _links.Validate(token);
        if (data == null) return BadRequest("This link is invalid or expired. Sign in to manage your booking.");
        var session = await _sessionService.GetSessionAsync(data.SessionId);
        return Ok(new { session = new { session.Id, session.SessionDate, session.StartTime, session.EndTime, session.Location, session.DropInPrice }, data.Attending });
    }

    public record ConfirmLinkRequest(string Token);

    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ApplyAttendanceLink([FromBody] ConfirmLinkRequest request)
    {
        var data = _links.Validate(request.Token);
        if (data == null) return BadRequest("This link is invalid or expired. Sign in to manage your booking.");
        try
        {
            var result = await _attendanceService.UpdateAttendanceAsync(data.SessionId, data.UserId, data.Attending, null, "Signed reminder link");
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Add multiple participants to a session (Admin only)
    /// </summary>
    [HttpPost("sessions/{sessionId}/add-participants")]
    [Authorize(Roles = "Admin")]
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
            var response = await _attendanceService.AddParticipantsToSessionAsync(sessionId, request);
            await _auditLogService.LogAsync("ParticipantsAdded", "Session", sessionId,
                $"{request.UserIds?.Count() ?? 0} player(s) added", User.AuditUserId(), User.AuditUserName());
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
    [Authorize(Roles = "Admin")]
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
            var response = await _attendanceService.RemoveParticipantsFromSessionAsync(sessionId, request);
            await _auditLogService.LogAsync("ParticipantsRemoved", "Session", sessionId,
                $"{request.UserIds?.Count() ?? 0} player(s) removed", User.AuditUserId(), User.AuditUserName());
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
