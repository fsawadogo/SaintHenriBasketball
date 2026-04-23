using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.EmergencyProfile;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.EmergencyProfile)]
public class EmergencyProfileController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<EmergencyProfileController> _logger;

    public EmergencyProfileController(IUserRepository userRepository, ILogger<EmergencyProfileController> logger)
    {
        _userRepository = userRepository;
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmergencyProfileDto>> UpdateOwn([FromBody] EmergencyProfileDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();

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
        return Ok(new EmergencyProfileDto
        {
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            MedicalAlerts = user.MedicalAlerts,
        });
    }
}
