using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification);
    Task<IReadOnlyList<Notification>> GetRecentAsync(Guid userId, int take);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<int> MarkAllAsReadAsync(Guid userId);
}
