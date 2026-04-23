using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.Application.DTOs.Notifications;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
public class NotificationPreferencesController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<NotificationPreferencesController> _logger;

    public NotificationPreferencesController(
        IUserRepository userRepository,
        ILogger<NotificationPreferencesController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/notification-preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();
        return Ok(ToDto(user.EmailNotificationsEnabled, user.SmsOptIn, user.PhoneNumber, user.InAppNotificationsEnabled));
    }

    [HttpPut("api/v{version:apiVersion}/users/me/notification-preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdateOwn([FromBody] NotificationPreferencesDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null) return NotFound();

        user.PhoneNumber = string.IsNullOrWhiteSpace(body.PhoneNumber) ? null : body.PhoneNumber.Trim();
        user.SmsOptIn = body.SmsOptIn && !string.IsNullOrEmpty(user.PhoneNumber);
        user.EmailNotificationsEnabled = body.EmailEnabled;
        user.InAppNotificationsEnabled = body.InAppEnabled;

        try
        {
            await _userRepository.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist notification preferences for {UserId}", userId);
            return StatusCode(500, ex.Message);
        }

        var refreshed = await _userRepository.GetByIdAsync(userId.Value);
        return Ok(ToDto(refreshed!.EmailNotificationsEnabled, refreshed.SmsOptIn, refreshed.PhoneNumber, refreshed.InAppNotificationsEnabled));
    }

    private static NotificationPreferencesDto ToDto(bool email, bool sms, string? phone, bool inApp) => new()
    {
        EmailEnabled = email,
        SmsOptIn = sms,
        PhoneNumber = phone,
        InAppEnabled = inApp,
    };
}
