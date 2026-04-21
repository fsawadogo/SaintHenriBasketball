using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.PromoCodes;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class PromoCodeService : IPromoCodeService
{
    private readonly IPromoCodeRepository _repository;
    private readonly ILogger<PromoCodeService> _logger;

    public PromoCodeService(IPromoCodeRepository repository, ILogger<PromoCodeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PromoCodeDto>> GetAllAsync()
    {
        var all = await _repository.GetAllAsync();
        return all.Select(ToDto).ToList();
    }

    public async Task<PromoCodeDto> CreateAsync(UpsertPromoCodeDto body)
    {
        Validate(body);
        var code = body.Code.Trim().ToUpperInvariant();
        if (await _repository.GetByCodeAsync(code) is not null)
            throw new ValidationException("A promo code with that value already exists");

        var entity = new PromoCode(code, body.DiscountType, body.DiscountValue,
            body.ValidFrom, body.ValidUntil, body.AppliesTo, body.MaxUses, body.IsActive);

        await _repository.AddAsync(entity);
        return ToDto(entity);
    }

    public async Task<PromoCodeDto> UpdateAsync(Guid id, UpsertPromoCodeDto body)
    {
        Validate(body);
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Promo code {id} not found");

        entity.DiscountType = body.DiscountType;
        entity.DiscountValue = body.DiscountValue;
        entity.ValidFrom = body.ValidFrom;
        entity.ValidUntil = body.ValidUntil;
        entity.AppliesTo = body.AppliesTo;
        entity.MaxUses = body.MaxUses;
        entity.IsActive = body.IsActive;

        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Promo code {id} not found");
        await _repository.DeleteAsync(entity.Id);
    }

    public async Task<ValidatePromoCodeResultDto> ValidateAsync(ValidatePromoCodeDto body)
    {
        var result = new ValidatePromoCodeResultDto
        {
            OriginalAmount = body.Amount,
            FinalAmount = body.Amount,
        };

        if (string.IsNullOrWhiteSpace(body.Code))
        {
            result.Reason = "Code is required";
            return result;
        }

        var code = body.Code.Trim().ToUpperInvariant();
        var promo = await _repository.GetByCodeAsync(code);
        if (promo is null) { result.Reason = "Code not found"; return result; }
        if (!promo.IsActive) { result.Reason = "Code is inactive"; return result; }

        var now = DateTime.UtcNow;
        if (now < promo.ValidFrom) { result.Reason = "Code is not yet valid"; return result; }
        if (now > promo.ValidUntil) { result.Reason = "Code has expired"; return result; }
        if (promo.MaxUses is int max && promo.TimesUsed >= max) { result.Reason = "Code has been used up"; return result; }

        if (promo.AppliesTo != PromoAppliesTo.Both && promo.AppliesTo != body.TargetPlan)
        {
            result.Reason = "Code does not apply to this plan";
            return result;
        }

        var discount = promo.DiscountType == PromoDiscountType.Percent
            ? Math.Round(body.Amount * (promo.DiscountValue / 100m), 2)
            : Math.Min(promo.DiscountValue, body.Amount);

        result.Valid = true;
        result.DiscountAmount = discount;
        result.FinalAmount = Math.Max(0, body.Amount - discount);
        return result;
    }

    private static void Validate(UpsertPromoCodeDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Code))
            throw new ValidationException("Code is required");
        if (body.DiscountValue <= 0)
            throw new ValidationException("Discount value must be positive");
        if (body.DiscountType == PromoDiscountType.Percent && body.DiscountValue > 100)
            throw new ValidationException("Percentage discount cannot exceed 100");
        if (body.ValidUntil <= body.ValidFrom)
            throw new ValidationException("Valid-until must be after valid-from");
    }

    private static PromoCodeDto ToDto(PromoCode p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        DiscountType = p.DiscountType,
        DiscountValue = p.DiscountValue,
        ValidFrom = p.ValidFrom,
        ValidUntil = p.ValidUntil,
        MaxUses = p.MaxUses,
        TimesUsed = p.TimesUsed,
        AppliesTo = p.AppliesTo,
        IsActive = p.IsActive,
        CreatedOn = p.CreatedOn,
    };
}
