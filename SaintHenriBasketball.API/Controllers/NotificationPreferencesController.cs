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
        return Ok(ToDto(user));
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
        user.SessionRemindersEnabled = body.SessionRemindersEnabled;
        user.PaymentRemindersEnabled = body.PaymentRemindersEnabled;
        user.WaitlistAlertsEnabled = body.WaitlistAlertsEnabled;
        user.CommunityUpdatesEnabled = body.CommunityUpdatesEnabled;


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
        return Ok(ToDto(refreshed!));
    }

    private static NotificationPreferencesDto ToDto(SaintHenriBasketball.Domain.Entities.ApplicationUser user) => new()
    {
        EmailEnabled = user.EmailNotificationsEnabled,
        SmsOptIn = user.SmsOptIn,
        PhoneNumber = user.PhoneNumber,
        InAppEnabled = user.InAppNotificationsEnabled,
        SessionRemindersEnabled = user.SessionRemindersEnabled,
        PaymentRemindersEnabled = user.PaymentRemindersEnabled,
        WaitlistAlertsEnabled = user.WaitlistAlertsEnabled,
        CommunityUpdatesEnabled = user.CommunityUpdatesEnabled,

    };
}
