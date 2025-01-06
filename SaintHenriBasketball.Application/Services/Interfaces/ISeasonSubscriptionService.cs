using SaintHenriBasketball.Application.DTOs;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISeasonSubscriptionService
{
    Task<SeasonSubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateSeasonSubscriptionDto createDto);
    Task<SeasonSubscriptionDto> GetSubscriptionAsync(Guid id);
    Task<SeasonSubscriptionDto> GetActiveSubscriptionAsync(Guid userId);
    Task<IReadOnlyList<SeasonSubscriptionDto>> GetAllSubscriptionsAsync();
    Task<IReadOnlyList<SeasonSubscriptionDto>> GetActiveSubscriptionsAsync();
    Task CancelSubscriptionAsync(Guid id);
    Task UpdatePaymentStatusAsync(Guid id, PaymentStatus status);
    Task<bool> HasActiveSubscriptionAsync(Guid userId);
}