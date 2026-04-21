using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IFeatureFlagRepository
{
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync();
    Task<FeatureFlag?> GetByKeyAsync(string key);
    Task AddAsync(FeatureFlag flag);
    Task UpdateAsync(FeatureFlag flag);
}
