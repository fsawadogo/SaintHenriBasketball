using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.FeatureFlags;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class FeatureFlagService : IFeatureFlagService
{
    private const string AllFlagsCacheKey = "FeatureFlags:All";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IFeatureFlagRepository _repository;
    private readonly ICacheService _cache;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(
        IFeatureFlagRepository repository,
        ICacheService cache,
        IAuditLogRepository auditLogRepository,
        ILogger<FeatureFlagService> logger)
    {
        _repository = repository;
        _cache = cache;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key)
    {
        var flags = await GetAllFlagsCachedAsync();
        return flags.TryGetValue(key, out var flag) && flag.Enabled;
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync()
    {
        var flags = await GetAllFlagsCachedAsync();
        return flags.Values.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetPublicFlagsAsync()
    {
        var flags = await GetAllFlagsCachedAsync();
        return flags.Values
            .Where(f => f.IsPublic)
            .ToDictionary(f => f.Key, f => f.Enabled);
    }

    public async Task<FeatureFlagDto> SetEnabledAsync(string key, bool enabled, Guid? adminId, string adminName)
    {
        var flag = await _repository.GetByKeyAsync(key)
            ?? throw new NotFoundException($"Feature flag '{key}' not found");

        var previousEnabled = flag.Enabled;
        flag.Enabled = enabled;
        flag.UpdatedBy = adminId;
        await _repository.UpdateAsync(flag);
        await _cache.RemoveAsync(AllFlagsCacheKey);

        if (previousEnabled != enabled)
        {
            await _auditLogRepository.AddAsync(new AuditLog(
                action: enabled ? "FeatureFlag.Enabled" : "FeatureFlag.Disabled",
                entityType: nameof(FeatureFlag),
                entityId: flag.Id,
                details: $"Key: {key}",
                userId: adminId,
                userName: adminName));

            _logger.LogInformation(
                "Feature flag {Key} toggled to {Enabled} by {AdminName}", key, enabled, adminName);
        }

        return ToDto(flag);
    }

    public async Task SeedDefaultsAsync(IEnumerable<FeatureFlagDefinition> definitions)
    {
        var existing = (await _repository.GetAllAsync()).ToDictionary(f => f.Key, f => f);

        foreach (var def in definitions)
        {
            if (existing.TryGetValue(def.Key, out var flag))
            {
                if (flag.Description != def.Description
                    || flag.DescriptionFr != def.DescriptionFr
                    || flag.IsPublic != def.IsPublic)
                {
                    flag.Description = def.Description;
                    flag.DescriptionFr = def.DescriptionFr;
                    flag.IsPublic = def.IsPublic;
                    await _repository.UpdateAsync(flag);
                }
            }
            else
            {
                await _repository.AddAsync(new FeatureFlag(
                    def.Key, def.Description, def.DescriptionFr, def.IsPublic, enabled: false));
                _logger.LogInformation("Seeded new feature flag {Key} (disabled by default)", def.Key);
            }
        }

        await _cache.RemoveAsync(AllFlagsCacheKey);
    }

    private async Task<IReadOnlyDictionary<string, FeatureFlag>> GetAllFlagsCachedAsync()
    {
        var cached = await _cache.GetAsync<Dictionary<string, FeatureFlag>>(AllFlagsCacheKey);
        if (cached is not null)
            return cached;

        var flags = (await _repository.GetAllAsync()).ToDictionary(f => f.Key);
        await _cache.SetAsync(AllFlagsCacheKey, flags, CacheTtl);
        return flags;
    }

    private static FeatureFlagDto ToDto(FeatureFlag flag) => new()
    {
        Key = flag.Key,
        Enabled = flag.Enabled,
        Description = flag.Description,
        DescriptionFr = flag.DescriptionFr,
        IsPublic = flag.IsPublic,
        UpdatedBy = flag.UpdatedBy,
        ModifiedOn = flag.ModifiedOn,
        CreatedOn = flag.CreatedOn,
    };
}
