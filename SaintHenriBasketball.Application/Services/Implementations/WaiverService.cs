using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Waivers;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class WaiverService : IWaiverService
{
    private readonly IWaiverRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<WaiverService> _logger;

    public WaiverService(
        IWaiverRepository repository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IFeatureFlagService featureFlagService,
        ILogger<WaiverService> logger)
    {
        _repository = repository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task EnsureAcceptedAsync(Guid userId)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Waiver)) return;
        var active = await _repository.GetActiveTemplateAsync();
        if (active is null) return;
        if (await _repository.GetAcceptanceAsync(userId, active.Version) is null)
            throw new ValidationException("Please review and accept the current waiver before booking or checking in.");
    }

    public async Task<CurrentWaiverDto> GetCurrentAsync(Guid userId)
    {
        var active = await _repository.GetActiveTemplateAsync();
        if (active is null)
            return new CurrentWaiverDto { Template = null, UserHasAccepted = true };

        var acceptance = await _repository.GetAcceptanceAsync(userId, active.Version);
        return new CurrentWaiverDto
        {
            Template = ToDto(active),
            UserHasAccepted = acceptance is not null,
        };
    }

    public async Task AcceptCurrentAsync(Guid userId, string? ipAddress)
    {
        var active = await _repository.GetActiveTemplateAsync()
            ?? throw new NotFoundException("No active waiver");

        if (await _repository.GetAcceptanceAsync(userId, active.Version) is not null)
            return; // idempotent

        await _repository.AddAcceptanceAsync(new WaiverAcceptance(userId, active.Version, ipAddress));
        _logger.LogInformation("Waiver v{Version} accepted by {UserId}", active.Version, userId);
    }

    public async Task<IReadOnlyList<WaiverTemplateDto>> GetAllTemplatesAsync()
    {
        var all = await _repository.GetAllTemplatesAsync();
        return all.Select(ToDto).ToList();
    }

    public async Task<WaiverTemplateDto> CreateTemplateAsync(CreateWaiverTemplateDto body)
    {
        if (string.IsNullOrWhiteSpace(body.BodyEn) || string.IsNullOrWhiteSpace(body.BodyFr))
            throw new ValidationException("Both English and French bodies are required");

        var existing = await _repository.GetAllTemplatesAsync();
        var nextVersion = existing.Any() ? existing.Max(t => t.Version) + 1 : 1;

        var template = new WaiverTemplate(
            nextVersion,
            body.BodyEn.Trim(),
            body.BodyFr.Trim(),
            body.EffectiveDate ?? DateTime.UtcNow,
            body.Activate);

        if (body.Activate)
        {
            foreach (var prior in existing.Where(t => t.IsActive))
            {
                prior.IsActive = false;
                await _repository.UpdateTemplateAsync(prior);
            }
        }

        await _repository.AddTemplateAsync(template);
        _logger.LogInformation("Waiver template v{Version} created (active={Active})", nextVersion, body.Activate);

        // Notify on activation so users see the prompt before their next visit
        // triggers the WaiverGuard modal blocking flow.
        if (body.Activate)
        {
            var allUsers = await _userRepository.GetAllUsersAsync();
            foreach (var user in allUsers.Where(u => u.EmailConfirmed))
            {
                await _notificationService.CreateAsync(
                    user.Id,
                    NotificationType.Generic,
                    title: EmailTemplateHelper.L("Updated waiver", "Décharge mise à jour", user.PreferredLanguage),
                    body: EmailTemplateHelper.L(
                        "A new waiver version is in effect. Please review and accept on your next visit.",
                        "Une nouvelle version de la décharge est en vigueur. Veuillez la consulter et l'accepter lors de votre prochaine visite.",
                        user.PreferredLanguage),
                    url: "/profile");
            }
        }

        return ToDto(template);
    }

    public async Task<WaiverAcceptancesDto> GetAcceptancesAsync(int version)
    {
        var acceptances = await _repository.GetAcceptancesAsync(version);
        var confirmedUsers = (await _userRepository.GetAllUsersAsync()).Where(u => u.EmailConfirmed).ToList();
        var usersById = confirmedUsers.ToDictionary(u => u.Id);
        var acceptedIds = acceptances.Select(a => a.UserId).ToHashSet();

        var rows = acceptances
            .Select(a =>
            {
                usersById.TryGetValue(a.UserId, out var user);
                return new WaiverAcceptanceDto
                {
                    UserId = a.UserId,
                    Name = user is null ? "(unconfirmed or deleted account)" : $"{user.FirstName} {user.LastName}".Trim(),
                    Email = user?.Email,
                    AcceptedAt = DateTime.SpecifyKind(a.AcceptedAt, DateTimeKind.Utc),
                };
            })
            .OrderByDescending(r => r.AcceptedAt)
            .ToList();

        return new WaiverAcceptancesDto
        {
            Version = version,
            AcceptedCount = rows.Count,
            PendingCount = confirmedUsers.Count(u => !acceptedIds.Contains(u.Id)),
            Acceptances = rows,
        };
    }

    private static WaiverTemplateDto ToDto(WaiverTemplate t) => new()
    {
        Id = t.Id,
        Version = t.Version,
        BodyEn = t.BodyEn,
        BodyFr = t.BodyFr,
        EffectiveDate = t.EffectiveDate,
        IsActive = t.IsActive,
        CreatedOn = t.CreatedOn,
    };
}
