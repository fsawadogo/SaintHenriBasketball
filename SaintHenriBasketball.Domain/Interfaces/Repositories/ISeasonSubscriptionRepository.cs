using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISeasonSubscriptionRepository
{
    Task<SeasonSubscription> GetByIdAsync(Guid id);
    Task<SeasonSubscription> GetActiveSubscriptionByUserIdAsync(Guid userId);
    Task<IReadOnlyList<SeasonSubscription>> GetAllAsync();
    Task<IReadOnlyList<SeasonSubscription>> GetActiveSubscriptionsAsync();
    Task AddAsync(SeasonSubscription subscription);
    Task UpdateAsync(SeasonSubscription subscription);
    Task<bool> HasActiveSubscriptionAsync(Guid userId);
}