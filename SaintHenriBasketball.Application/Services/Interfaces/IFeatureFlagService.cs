using SaintHenriBasketball.Application.DTOs.FeatureFlags;
using SaintHenriBasketball.Application.FeatureFlags;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key);
    Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync();
    Task<IReadOnlyDictionary<string, bool>> GetPublicFlagsAsync();
    /// Flags for the client app; admin-only flags are included only when requested for an admin.
    Task<IReadOnlyDictionary<string, bool>> GetClientFlagsAsync(bool includeAdminOnly);
    Task<FeatureFlagDto> SetEnabledAsync(string key, bool enabled, Guid? adminId, string adminName);
    Task<IReadOnlyList<FeatureFlagDto>> SeedDefaultsAsync(IEnumerable<FeatureFlagDefinition> definitions);
}
