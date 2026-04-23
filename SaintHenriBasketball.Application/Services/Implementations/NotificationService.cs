using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Notifications;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class NotificationService : INotificationService
{
    private const int MaxRecentDefault = 50;

    private readonly INotificationRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        IUserRepository userRepository,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task CreateAsync(Guid userId, NotificationType type, string title, string body, string? url = null)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null) return;
            if (!user.InAppNotificationsEnabled) return;

            await _repository.AddAsync(new Notification(userId, type, title, body, url));
        }
        catch (Exception ex)
        {
            // Notifications must never break the calling flow (e.g. a payment confirmation).
            _logger.LogError(ex, "Failed to create in-app notification for user {UserId} type {Type}", userId, type);
        }
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(Guid userId, int take = 20)
    {
        var clamped = Math.Clamp(take, 1, MaxRecentDefault);
        var items = await _repository.GetRecentAsync(userId, clamped);
        return items.Select(ToDto).ToList();
    }

    public Task<int> GetUnreadCountAsync(Guid userId) => _repository.GetUnreadCountAsync(userId);

    public Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId) =>
        _repository.MarkAsReadAsync(userId, notificationId);

    public Task<int> MarkAllAsReadAsync(Guid userId) => _repository.MarkAllAsReadAsync(userId);

    private static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        Type = (int)n.Type,
        Title = n.Title,
        Body = n.Body,
        Url = n.Url,
        ReadAt = n.ReadAt,
        CreatedOn = n.CreatedOn,
    };
}
