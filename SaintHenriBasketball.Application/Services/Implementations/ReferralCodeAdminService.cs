using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.PromoReferralReports;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class ReferralCodeAdminService : IReferralCodeAdminService
{
    public const string MaxUsesMessage = "Max uses must be at least 1, or left empty for unlimited.";
    public const string NotFoundMessage = "Referral code not found";

    public static string BelowUsesMessage(int uses) =>
        $"This code has already been used {uses} time(s). Set max uses to at least {uses}, or confirm that new redemptions should simply be blocked.";

    private readonly IReferralRepository _repository;
    private readonly IAuditLogService _audit;
    private readonly ILogger<ReferralCodeAdminService> _logger;

    public ReferralCodeAdminService(IReferralRepository repository, IAuditLogService audit, ILogger<ReferralCodeAdminService> logger)
    {
        _repository = repository;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ReferralCodeAdminPageDto> SearchAsync(ReferralCodeAdminQuery query)
    {
        var (page, pageSize) = ListPaging.Clamp(query.Page, query.PageSize);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var result = await _repository.SearchCodesAsync(new ReferralCodeSearchCriteria(search, page, pageSize));
        return new ReferralCodeAdminPageDto { Items = result.Items.Select(ToDto).ToList(), Total = result.Total, Page = page, PageSize = pageSize };
    }

    public async Task<ReferralCodeAdminDto> UpdateAsync(Guid id, UpdateReferralCodeDto body, Guid? adminId, string adminName)
    {
        if (body.MaxUses is int max && max < 1)
            throw new ValidationException(MaxUsesMessage);

        var before = await _repository.GetCodeByIdAsync(id) ?? throw new NotFoundException(NotFoundMessage);
        if (body.MaxUses is int limit && limit < before.TimesUsed && !body.AllowBelowCurrentUses)
            throw new ValidationException(BelowUsesMessage(before.TimesUsed));

        switch (await _repository.TryUpdateCodeLimitsAsync(id, body.IsActive, body.MaxUses, body.AllowBelowCurrentUses))
        {
            case ReferralCodeUpdateOutcome.NotFound:
                throw new NotFoundException(NotFoundMessage);
            case ReferralCodeUpdateOutcome.BelowCurrentUses:
                // A redemption counted a use between the read and the update.
                var current = await _repository.GetCodeByIdAsync(id);
                throw new ValidationException(BelowUsesMessage(current?.TimesUsed ?? before.TimesUsed));
        }

        var row = await _repository.GetCodeAdminRowAsync(id) ?? throw new NotFoundException(NotFoundMessage);
        var details = $"Code: {before.Code}; IsActive: {Flag(before.IsActive)} -> {Flag(body.IsActive)}; "
            + $"MaxUses: {Limit(before.MaxUses)} -> {Limit(body.MaxUses)}; Uses: {row.Code.TimesUsed}";
        if (body.MaxUses is int newMax && newMax < row.Code.TimesUsed)
            details += "; MaxUses set below current uses (new redemptions blocked)";
        try
        {
            await _audit.LogAsync("ReferralCode.Updated", nameof(ReferralCode), id, details, adminId, adminName);
        }
        catch (Exception ex)
        {
            // The change is already saved; a missing audit row must not report it as failed.
            _logger.LogWarning(ex, "Audit entry failed for referral code {CodeId}: {Details}", id, details);
        }

        return ToDto(row);
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Limit(int? value) => value?.ToString() ?? "unlimited";

    private static ReferralCodeAdminDto ToDto(ReferralCodeAdminRow row) => new()
    {
        Id = row.Code.Id,
        Code = row.Code.Code,
        OwnerUserId = row.Code.OwnerUserId,
        OwnerName = row.OwnerFirstName is null && row.OwnerLastName is null
            ? "(unknown)"
            : $"{row.OwnerFirstName} {row.OwnerLastName}".Trim(),
        OwnerEmail = row.OwnerEmail,
        IsActive = row.Code.IsActive,
        MaxUses = row.Code.MaxUses,
        TimesUsed = row.Code.TimesUsed,
        Redemptions = new ReferralRedemptionCountsDto
        {
            Pending = row.PendingRedemptions,
            Granted = row.GrantedRedemptions,
            Revoked = row.RevokedRedemptions,
            Total = row.PendingRedemptions + row.GrantedRedemptions + row.RevokedRedemptions,
        },
        RewardsGrantedCount = row.RewardsGrantedCount,
        RewardsGrantedTotal = row.RewardsGrantedTotal,
        CreatedOn = DateTime.SpecifyKind(row.Code.CreatedOn, DateTimeKind.Utc),
    };
}
