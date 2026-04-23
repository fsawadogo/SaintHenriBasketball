using SaintHenriBasketball.Application.DTOs.Notifications;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface INotificationService
{
    /// Creates an in-app notification for the user. Silently no-ops when the user
    /// has opted out via InAppNotificationsEnabled. Safe for callers to fire-and-forget.
    Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? url = null);

    Task<IReadOnlyList<NotificationDto>> GetRecentAsync(Guid userId, int take = 20);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<int> MarkAllAsReadAsync(Guid userId);
}
