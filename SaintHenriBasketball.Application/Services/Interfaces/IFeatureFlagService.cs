using SaintHenriBasketball.Application.DTOs.FeatureFlags;
using SaintHenriBasketball.Application.FeatureFlags;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key);
    Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync();
    Task<IReadOnlyDictionary<string, bool>> GetPublicFlagsAsync();
    Task<FeatureFlagDto> SetEnabledAsync(string key, bool enabled, Guid? adminId, string adminName);
    Task SeedDefaultsAsync(IEnumerable<FeatureFlagDefinition> definitions);
}
