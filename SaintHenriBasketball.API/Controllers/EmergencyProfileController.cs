using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.EmergencyProfile;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.EmergencyProfile)]
public class EmergencyProfileController : BaseApiController
{
    private const int MaxContactNameLength = 200;
    private const int MaxContactPhoneLength = 40;
    private const int MaxMedicalAlertsLength = 2000;

    private readonly IUserRepository _userRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<EmergencyProfileController> _logger;

    public EmergencyProfileController(IUserRepository userRepository, IAuditLogService auditLogService, ILogger<EmergencyProfileController> logger)
    {
        _userRepository = userRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/emergency-profile")]
    [ProducesResponseType(typeof(EmergencyProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmergencyProfileDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        return Ok(new EmergencyProfileDto
        {
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            MedicalAlerts = user.MedicalAlerts,
        });
    }

    [HttpPut("api/v{version:apiVersion}/users/me/emergency-profile")]
    [ProducesResponseType(typeof(EmergencyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmergencyProfileDto>> UpdateOwn([FromBody] EmergencyProfileDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();

        // Limits match the column lengths in ApplicationDbContext; longer input used to fail as a 500.
        if (body.EmergencyContactName?.Trim().Length > MaxContactNameLength)
            return BadRequest($"Emergency contact name must be {MaxContactNameLength} characters or fewer.");
        if (body.EmergencyContactPhone?.Trim().Length > MaxContactPhoneLength)
            return BadRequest($"Emergency contact phone must be {MaxContactPhoneLength} characters or fewer.");
        if (body.MedicalAlerts?.Trim().Length > MaxMedicalAlertsLength)
            return BadRequest($"Medical alerts must be {MaxMedicalAlertsLength} characters or fewer.");

        user.EmergencyContactName = string.IsNullOrWhiteSpace(body.EmergencyContactName) ? null : body.EmergencyContactName.Trim();
        user.EmergencyContactPhone = string.IsNullOrWhiteSpace(body.EmergencyContactPhone) ? null : body.EmergencyContactPhone.Trim();
        user.MedicalAlerts = string.IsNullOrWhiteSpace(body.MedicalAlerts) ? null : body.MedicalAlerts.Trim();

        try
        {
            await _userRepository.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist emergency profile for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }

        // Re-fetch so the response reflects what's actually in the DB, not the request echo.
        var refreshed = await _userRepository.GetByIdAsync(userId.Value);
        return Ok(new EmergencyProfileDto
        {
            EmergencyContactName = refreshed?.EmergencyContactName,
            EmergencyContactPhone = refreshed?.EmergencyContactPhone,
            MedicalAlerts = refreshed?.MedicalAlerts,
        });
    }

    [HttpGet("api/v{version:apiVersion}/admin/users/{userId:guid}/emergency-profile")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmergencyProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmergencyProfileDto>> GetForUser(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null) return NotFound();

        // Health and contact details: every admin read is recorded in the audit log.
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
        await _auditLogService.LogAsync("EmergencyProfile.Viewed", "User", userId, null, GetUserId(), adminName);
        return Ok(new EmergencyProfileDto
        {
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            MedicalAlerts = user.MedicalAlerts,
        });
    }
}
