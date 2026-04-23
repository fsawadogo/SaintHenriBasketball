using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Notifications;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.InAppNotifications)]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/notifications")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetRecent([FromQuery] int take = 20)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var items = await _notificationService.GetRecentAsync(userId.Value, take);
        return Ok(items);
    }

    [HttpGet("api/v{version:apiVersion}/users/me/notifications/unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var count = await _notificationService.GetUnreadCountAsync(userId.Value);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPost("api/v{version:apiVersion}/users/me/notifications/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await _notificationService.MarkAsReadAsync(userId.Value, id);
        return NoContent();
    }

    [HttpPost("api/v{version:apiVersion}/users/me/notifications/read-all")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var marked = await _notificationService.MarkAllAsReadAsync(userId.Value);
        return Ok(new UnreadCountDto { Count = marked });
    }
}
