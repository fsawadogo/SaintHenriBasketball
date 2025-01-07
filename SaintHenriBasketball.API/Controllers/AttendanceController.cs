using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs;
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
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService;
        _logger = logger;
    }

    /// <summary>
    /// Mark user attendance for a session (Admin only)
    /// </summary>
    [HttpPost("sessions/{sessionId}/users/{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AttendanceDto>> MarkAttendance(
        Guid sessionId,
        Guid userId,
        [FromBody] bool isPresent,
        [FromQuery] string? notes = null)
    {
        try
        {
            var attendance = await _attendanceService.MarkAttendanceAsync(sessionId, userId, isPresent, notes);
            return Ok(attendance);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get attendance summary for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionAttendanceSummaryDto>> GetSessionAttendance(Guid sessionId)
    {
        var summary = await _attendanceService.GetSessionAttendanceSummaryAsync(sessionId);
        return Ok(summary);
    }

    /// <summary>
    /// Get attendance history for a user
    /// </summary>
    [HttpGet("users/{userId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetUserAttendanceHistory(Guid userId)
    {
        var history = await _attendanceService.GetUserAttendanceHistoryAsync(userId);
        return Ok(history);
    }

    /// <summary>
    /// Get my attendance history
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetMyAttendanceHistory()
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
    /// Get attendance statistics for a date range (Admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceStatsDto>> GetAttendanceStats(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var stats = await _attendanceService.GetAttendanceStatsAsync(startDate, endDate);
        return Ok(stats);
    }
}
